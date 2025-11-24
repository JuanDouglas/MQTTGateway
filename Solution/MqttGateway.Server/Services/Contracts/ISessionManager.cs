namespace MqttGateway.Server.Services.Contracts;

public interface ISessionManager
{
    Task<bool> SubscribeContext(Guid sessionId, string connectionId, CancellationToken stoppingToken = default);
    Task<bool> RemoveConnectionAsync(Guid sessionId, string connectionId, CancellationToken stoppingToken = default);
    HashSet<string> RelayClients(Guid sessionId);
}