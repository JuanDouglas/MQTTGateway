using MQTTnet;

namespace MqttGateway.Tests.Helpers;

/// <summary>
/// Helper para testes do cliente MQTT
/// </summary>
public class MqttTestClient : IAsyncDisposable
{
    private IMqttClient? _client;
    private readonly List<MqttApplicationMessage> _receivedMessages = new();
    private bool _disposed = false;

    public IReadOnlyList<MqttApplicationMessage> ReceivedMessages => _receivedMessages.AsReadOnly();
    public bool IsConnected => _client?.IsConnected ?? false;
    public string? ClientId { get; private set; }

    /// <summary>
    /// Conecta ao broker MQTT
    /// </summary>
    public async Task ConnectAsync(string server, int port, string? clientId = null, string? username = null, string? password = null)
    {
        ClientId = clientId ?? Guid.NewGuid().ToString();

        var factory = new MqttClientFactory();
        _client = factory.CreateMqttClient();

        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithTcpServer(server, port)
            .WithClientId(ClientId)
            .WithCleanSession(true);

        if (!string.IsNullOrEmpty(username))
        {
            optionsBuilder = optionsBuilder.WithCredentials(username, password);
        }

        var options = optionsBuilder.Build();

        // Configurar handler para mensagens recebidas
        _client.ApplicationMessageReceivedAsync += OnMessageReceived;

        await _client.ConnectAsync(options);
    }

    /// <summary>
    /// Desconecta do broker MQTT
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_client != null && _client.IsConnected)
        {
            await _client.DisconnectAsync();
        }
    }

    /// <summary>
    /// Subscreve a um tópico
    /// </summary>
    public async Task SubscribeAsync(string topic, int qos = 1)
    {
        if (_client == null || !_client.IsConnected)
            throw new InvalidOperationException("Client is not connected");

        var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(topic, (MQTTnet.Protocol.MqttQualityOfServiceLevel)qos)
            .Build();

        await _client.SubscribeAsync(subscribeOptions);
    }

    /// <summary>
    /// Cancela subscrição de um tópico
    /// </summary>
    public async Task UnsubscribeAsync(string topic)
    {
        if (_client == null || !_client.IsConnected)
            throw new InvalidOperationException("Client is not connected");

        var unsubscribeOptions = new MqttClientUnsubscribeOptionsBuilder()
            .WithTopicFilter(topic)
            .Build();

        await _client.UnsubscribeAsync(unsubscribeOptions);
    }

    /// <summary>
    /// Publica uma mensagem
    /// </summary>
    public async Task PublishAsync(string topic, string payload, int qos = 1, bool retain = false)
    {
        if (_client == null || !_client.IsConnected)
            throw new InvalidOperationException("Client is not connected");

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel((MQTTnet.Protocol.MqttQualityOfServiceLevel)qos)
            .WithRetainFlag(retain)
            .Build();

        await _client.PublishAsync(message);
    }

    /// <summary>
    /// Publica uma mensagem com payload binário
    /// </summary>
    public async Task PublishAsync(string topic, byte[] payload, int qos = 1, bool retain = false)
    {
        if (_client == null || !_client.IsConnected)
            throw new InvalidOperationException("Client is not connected");

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel((MQTTnet.Protocol.MqttQualityOfServiceLevel)qos)
            .WithRetainFlag(retain)
            .Build();

        await _client.PublishAsync(message);
    }

    /// <summary>
    /// Aguarda receber uma mensagem específica
    /// </summary>
    public async Task<MqttApplicationMessage?> WaitForMessageAsync(
        Func<MqttApplicationMessage, bool> predicate,
        TimeSpan timeout)
    {
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < timeout)
        {
            var message = _receivedMessages.FirstOrDefault(predicate);
            if (message != null)
                return message;

            await Task.Delay(100);
        }

        return null;
    }

    /// <summary>
    /// Aguarda receber uma mensagem em um tópico específico
    /// </summary>
    public async Task<MqttApplicationMessage?> WaitForTopicMessageAsync(string topic, TimeSpan timeout)
    {
        return await WaitForMessageAsync(m => m.Topic == topic, timeout);
    }

    /// <summary>
    /// Limpa mensagens recebidas
    /// </summary>
    public void ClearReceivedMessages()
    {
        _receivedMessages.Clear();
    }

    /// <summary>
    /// Aguarda estar conectado
    /// </summary>
    public async Task<bool> WaitForConnectionAsync(TimeSpan timeout)
    {
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < timeout)
        {
            if (IsConnected)
                return true;

            await Task.Delay(100);
        }

        return false;
    }

    private Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs args)
    {
        _receivedMessages.Add(args.ApplicationMessage);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await DisconnectAsync();
            _client?.Dispose();
            _disposed = true;
        }
    }
}