using MQTTnet;
using MQTTnet.Server;
using System.Net;

namespace MqttGateway.Tests.Fixtures;

/// <summary>
/// Logger simples para testes que não faz nada
/// </summary>
public class TestLogger : MQTTnet.Diagnostics.Logger.IMqttNetLogger
{
    public bool IsEnabled => false;

    public void Publish(MQTTnet.Diagnostics.Logger.MqttNetLogLevel logLevel, string source, string message, object[]? parameters, Exception? exception)
    {
        // Não faz nada para testes
    }
}

/// <summary>
/// Fixture para criar um servidor MQTT de teste em memória
/// </summary>
public class TestMqttServerFixture : IAsyncDisposable
{
    private MqttServer? _mqttServer;
    private bool _disposed = false;

    public int Port { get; private set; }
    public string ConnectionString => $"Server=localhost;Port={Port};CleanSession=true";
    public bool IsStarted => _mqttServer?.IsStarted ?? false;

    /// <summary>
    /// Inicia o servidor MQTT de teste
    /// </summary>
    public async Task StartAsync()
    {
        if (_mqttServer != null)
            return;

        // Encontrar uma porta disponível
        Port = FindAvailablePort();

        var mqttServerOptions = new MqttServerOptionsBuilder()
            .WithDefaultEndpoint()
            .WithDefaultEndpointPort(Port)
            .Build();

        // Criando servidor com logger nulo
        _mqttServer = new MqttServer(mqttServerOptions, Enumerable.Empty<IMqttServerAdapter>(), new TestLogger());

        await _mqttServer.StartAsync();
    }

    /// <summary>
    /// Para o servidor MQTT
    /// </summary>
    public async Task StopAsync()
    {
        if (_mqttServer != null && _mqttServer.IsStarted)
        {
            await _mqttServer.StopAsync();
        }
    }

    /// <summary>
    /// Publica uma mensagem no servidor de teste
    /// </summary>
    public async Task PublishAsync(string topic, string payload, int qos = 1)
    {
        if (_mqttServer == null || !_mqttServer.IsStarted)
            throw new InvalidOperationException("MQTT Server is not started");

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel((MQTTnet.Protocol.MqttQualityOfServiceLevel)qos)
            .Build();

        await _mqttServer.InjectApplicationMessage(
            new InjectedMqttApplicationMessage(message)
            {
                SenderClientId = "test-server"
            });
    }

    private static int FindAvailablePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await StopAsync();
            _mqttServer?.Dispose();
            _disposed = true;
        }
    }
}