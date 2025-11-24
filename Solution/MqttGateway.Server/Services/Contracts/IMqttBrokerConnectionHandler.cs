namespace MqttGateway.Server.Services.Contracts;

public interface IMqttBrokerConnectionHandler
{
    Task SubscribeClientAsync(Guid hubId, Guid sessionId, CancellationToken stoppingToken = default);
    Task UnsubscribeClientAsync(Guid sessionId, CancellationToken stoppingToken = default);
    void SetDispatcher(IMqttEventDispatcher dispatcher);
}

public interface IMqttMessageDispatcher
{
    Task PublishMessageAsync(Guid sessionId, string payload, string? channel = null, CancellationToken stoppingToken = default);
    Task PublishDirectMessageAsync(Guid sessionId, Guid targetId, string payload, string? channel = null, CancellationToken stoppingToken = default);
}

public interface IMqttEventDispatcher
{
    void DispatchEvent(Guid sessionId, string payload, string? channel = null);
}
