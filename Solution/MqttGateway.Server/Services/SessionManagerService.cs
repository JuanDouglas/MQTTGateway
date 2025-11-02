using MqttGateway.Server.Services.Contracts;
using System.Collections.Concurrent;

namespace MqttGateway.Server.Services;

public class SessionManagerService : ISessionManager
{
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _relays = new();
    private readonly IMqttBrokerConnectionHandler _mqttConnectionHandler;
    private readonly ISessionContextStore _sessionContextStore;
    private readonly SemaphoreSlim _sessionLock = new(1, 1);

    public SessionManagerService(
        IMqttBrokerConnectionHandler mqttBrokerConnectionHandler,
        ISessionContextStore sessionContextStore)
    {
        _mqttConnectionHandler = mqttBrokerConnectionHandler;
        _sessionContextStore = sessionContextStore;
    }

    public HashSet<string> RelayClients(Guid sessionId)
    {
        if (!_relays.TryGetValue(sessionId, out var clients))
            return [];

        lock (clients) 
        {
            return [.. clients];
        }
    }

    public async Task<bool> RemoveConnectionAsync(Guid sessionId, string connectionId, CancellationToken stoppingToken = default)
    {
        await _sessionLock.WaitAsync(stoppingToken);

        try
        {
            if (!_relays.TryGetValue(sessionId, out var relayedConnections))
                return false;

            bool removed;
            lock (relayedConnections)
            {
                removed = relayedConnections.Remove(connectionId);
            }

            if (relayedConnections.Count < 1)
            {
                await _mqttConnectionHandler.UnsubscribeClientAsync(sessionId, stoppingToken);
                _sessionContextStore.RemoveContext(sessionId);
                _relays.TryRemove(sessionId, out _);
            }

            return removed;
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    public async Task<bool> SubscribeContext(Guid sessionId, string connectionId, CancellationToken stoppingToken = default)
    {
        await _sessionLock.WaitAsync(stoppingToken);

        try
        {
            if (!_relays.TryGetValue(sessionId, out HashSet<string>? relayedConnections))
            {
                await _mqttConnectionHandler.SubscribeClientAsync(Guid.Empty, sessionId, stoppingToken);
                relayedConnections = [];
                _relays[sessionId] = relayedConnections;
            }

            lock (relayedConnections)
            {
                return relayedConnections.Add(connectionId);
            }
        }
        finally
        {
            _sessionLock.Release();
        }
    }
}