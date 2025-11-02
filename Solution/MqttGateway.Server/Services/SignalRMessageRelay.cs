using Microsoft.AspNetCore.SignalR;
using MqttGateway.Server.Hubs;
using MqttGateway.Server.Objects;
using MqttGateway.Server.Services.Contracts;

namespace MqttGateway.Server.Services;

public class SignalRMessageRelay : IMqttEventDispatcher
{
    private const string CommandMethod = "ReceiveMessage";

    private readonly ISessionManager _sessionManager;
    private readonly ISessionContextStore _sessionContextStore;
    private readonly IHubContext<UserHub> _hubContext;

    public SignalRMessageRelay(
        ISessionManager sessionManager,
        ISessionContextStore sessionContextStore,
        IHubContext<UserHub> hubContext)
    {
        _sessionManager = sessionManager;
        _sessionContextStore = sessionContextStore;
        _hubContext = hubContext;
    }

    public void DispatchEvent(
        Guid sessionId,
        string payload,
        string? channel = null)
    {
        SessionContext? context = _sessionContextStore.GetContext(sessionId);

        if (context == null)
            _sessionContextStore.CreateContext(sessionId, payload);
        else
            context.IncressPayload(payload, channel);

        var clients = _sessionManager.RelayClients(sessionId);

        foreach (var item in clients)
        {
            _hubContext.Clients.Client(item)
                .SendAsync(
                CommandMethod,
                payload).Wait();
        }
    }
}