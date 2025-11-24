using Microsoft.AspNetCore.SignalR;
using MqttGateway.Server.Objects;
using MqttGateway.Server.Services.Contracts;
using System.Text.Json;

namespace MqttGateway.Server.Hubs;

public class UserHub : Hub
{
    public const string HubUrl = "/hub";
    private readonly ISessionManager _sessionManager;
    private readonly ISessionContextStore _sessionContextStore;

    public UserHub(
        ISessionManager sessionManager,
        ISessionContextStore sessionContextStore)
    {
        _sessionManager = sessionManager;
        _sessionContextStore = sessionContextStore;
    }

    public override async Task OnConnectedAsync()
    {
        if (!TryGetSessionId(out Guid sessionId) ||
            !SessionExists(sessionId))
        {
            Context.Abort();
            return;
        }

        await _sessionManager.SubscribeContext(sessionId, Context.ConnectionId);

        bool created = false;
        SessionContext? sessionContext;

        do
        {
            sessionContext = _sessionContextStore.GetContext(sessionId);

            if (sessionContext != null)
                created = true;

            Thread.Sleep(250);
        } while (!created);

        await Clients.Caller.SendAsync("SetContext", JsonSerializer.Serialize(sessionContext), CancellationToken.None);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (TryGetSessionId(out Guid sessionId))
        {
            _sessionManager.RemoveConnectionAsync(sessionId, Context.ConnectionId);
        }

        return Task.CompletedTask;
    }

    private bool TryGetSessionId(out Guid sessionId)
    {
        var httpContext = Context.GetHttpContext();
        var strSessionId = httpContext?.Request.Query["sessionId"].ToString();

        sessionId = default;

        return !string.IsNullOrWhiteSpace(strSessionId) && Guid.TryParse(strSessionId, out sessionId);
    }

    private bool SessionExists(Guid sessionId)
    {
        return true;
    }
}