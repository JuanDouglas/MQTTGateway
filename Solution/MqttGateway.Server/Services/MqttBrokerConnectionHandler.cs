using Microsoft.Extensions.Internal;
using MqttGateway.Server.Objects;
using MqttGateway.Server.Services.Contracts;
using MQTTnet;
using MQTTnet.Protocol;
using System.Collections.Concurrent;
using System.Text;

namespace MqttGateway.Server.Services;

public class MqttBrokerConnectionHandler : IMqttBrokerConnectionHandler, IMqttMessageDispatcher
{
    private readonly ConcurrentDictionary<Guid, Guid> _sessionClients = new();
    private readonly MqttConnectionStringBuilder _connectionStringBuilder;
    private readonly ISystemClock _systemClock;
    private IMqttEventDispatcher? _mqttEventDispatcher;
    private readonly IMqttClient mqttClient;
    private readonly SemaphoreSlim _mqttSemaphore = new(1, 1); // garante 1 operação por vez

    private const string baseTopic = "gateway";

    public MqttBrokerConnectionHandler(
        IConfiguration configuration,
        ISystemClock systemClock)
    {
        _systemClock = systemClock;
        var mqttConnectionString = configuration.GetConnectionString("MqttBroker");

        _connectionStringBuilder = new()
        {
            ConnectionString = mqttConnectionString ?? throw new ArgumentNullException(nameof(mqttConnectionString))
        };

        var factory = new MqttClientFactory();
        mqttClient = factory.CreateMqttClient();

        var optionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(_connectionStringBuilder.Server, _connectionStringBuilder.Port)
                .WithCleanSession(_connectionStringBuilder.CleanSession);

        if (_connectionStringBuilder.TrustedConnection.HasValue)
        {
            if (!_connectionStringBuilder.TrustedConnection.Value)
            {
                optionsBuilder = optionsBuilder.WithCredentials(_connectionStringBuilder.User, _connectionStringBuilder.Password);
            }
            else
            {
                optionsBuilder = optionsBuilder.WithTlsOptions(new MqttClientTlsOptions());
            }
        }
        else if (!string.IsNullOrWhiteSpace(_connectionStringBuilder.User))
        {
            optionsBuilder = optionsBuilder.WithCredentials(_connectionStringBuilder.User, _connectionStringBuilder.Password);
        }

        mqttClient.ApplicationMessageReceivedAsync += HandlerMessageReceivedAsync;
        mqttClient.ConnectAsync(optionsBuilder.Build()).Wait();
    }

    public async Task SubscribeClientAsync(Guid clientId, Guid sessionId, CancellationToken stoppingToken = default)
    {
        if (_sessionClients.ContainsKey(sessionId))
            return;

        _sessionClients[sessionId] = clientId;

        try
        {
            await _mqttSemaphore.WaitAsync(stoppingToken);
            await mqttClient.SubscribeAsync(GetTopicBySessionId(sessionId), cancellationToken: stoppingToken);
        }
        catch
        {
            _sessionClients.TryRemove(sessionId, out _);
        }
        finally
        {
            _mqttSemaphore.Release();
        }
    }

    public async Task UnsubscribeClientAsync(Guid sessionId, CancellationToken stoppingToken = default)
    {
        if (!_sessionClients.ContainsKey(sessionId))
            return;

        try
        {
            await _mqttSemaphore.WaitAsync(stoppingToken);
            await mqttClient.UnsubscribeAsync(GetTopicBySessionId(sessionId), cancellationToken: stoppingToken);
            _sessionClients.TryRemove(sessionId, out _);
        }
        finally
        {
            _mqttSemaphore.Release();
        }
    }

    public async Task PublishMessageAsync(Guid sessionId, string payload, string? channel = null, CancellationToken stoppingToken = default)
    {
        var mqttMessage = GetBuilder(sessionId, payload, channel)
            .Build();

        await _mqttSemaphore.WaitAsync(stoppingToken);

        try
        {
            await mqttClient.PublishAsync(mqttMessage, stoppingToken);
        }
        finally
        {
            _mqttSemaphore.Release();
        }
    }

    public async Task PublishDirectMessageAsync(
        Guid sessionId,
        Guid targetId,
        string payload,
        string? channel = null,
        CancellationToken stoppingToken = default)
    {
        var mqttMessage = GetBuilder(sessionId, payload, channel)
            .WithUserProperty("x-target-id", targetId.ToString())
            .Build();

        await _mqttSemaphore.WaitAsync(stoppingToken);

        try
        {
            await mqttClient.PublishAsync(mqttMessage, stoppingToken);
        }
        finally
        {
            _mqttSemaphore.Release();
        }
    }

    public void SetDispatcher(IMqttEventDispatcher dispatcher)
        => _mqttEventDispatcher = dispatcher;

    private Task HandlerMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        var topic = args.ApplicationMessage.Topic;
        var payload = args.ApplicationMessage.Payload.Length > 0 ? Encoding.UTF8.GetString(args.ApplicationMessage.Payload) : string.Empty;

        topic = topic[(baseTopic.Length + 1)..];

        var subtopics = topic.Split('/');
        if (subtopics.Length < 2 || !Guid.TryParse(subtopics[1], out Guid sessionId))
            return Task.CompletedTask;

        _mqttEventDispatcher?.DispatchEvent(sessionId, payload, subtopics.Last());
        return Task.CompletedTask;
    }

    private string GetTopicBySessionId(Guid sessionId, string? channel = null)
    {
        if (!_sessionClients.TryGetValue(sessionId, out Guid clientId))
            throw new KeyNotFoundException($"SessionId {sessionId} not found");

        string topic = $"{baseTopic}/{clientId}/{sessionId}";
        return string.IsNullOrWhiteSpace(channel) ? topic : $"{topic}/{channel}";
    }

    private MqttApplicationMessageBuilder GetBuilder(
        Guid sessionId,
        string payload,
        string? channel = null,
        MqttQualityOfServiceLevel qualityOfService = MqttQualityOfServiceLevel.ExactlyOnce)
    {
        return new MqttApplicationMessageBuilder()
                    .WithTopic(GetTopicBySessionId(sessionId, channel))
                    .WithPayload(payload)
                    .WithQualityOfServiceLevel(qualityOfService)
                    .WithUserProperty("source-service", "Gateway Service")
                    .WithUserProperty("timestamp-utc", _systemClock.UtcNow.ToString("D"));
    }
}