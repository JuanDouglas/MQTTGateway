using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;

namespace MqttGateway.Tests.Helpers;

/// <summary>
/// Helper para testes do SignalR Hub
/// </summary>
public class SignalRTestHelper : IAsyncDisposable
{
    private HubConnection? _connection;
    private readonly List<object> _receivedMessages = new();
    private readonly List<object> _receivedContexts = new();
    private bool _disposed = false;

    public IReadOnlyList<object> ReceivedMessages => _receivedMessages.AsReadOnly();
    public IReadOnlyList<object> ReceivedContexts => _receivedContexts.AsReadOnly();
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    /// <summary>
    /// Conecta ao hub SignalR com uma sessão específica
    /// </summary>
    public async Task ConnectAsync(string hubUrl, Guid sessionId, Guid? userId = null)
    {
        var url = $"{hubUrl}?sessionId={sessionId}";

        _connection = new HubConnectionBuilder()
            .WithUrl(url, options =>
            {
                options.HttpMessageHandlerFactory = _ => new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true
                };
            })
            .Build();

        // Configurar handlers para mensagens
        _connection.On<object>("SetContext", context =>
        {
            _receivedContexts.Add(context);
        });

        _connection.On<object>("ReceiveMessage", message =>
        {
            _receivedMessages.Add(message);
        });

        await _connection.StartAsync();
    }

    /// <summary>
    /// Desconecta do hub
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_connection != null)
        {
            await _connection.StopAsync();
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    /// <summary>
    /// Invoca um método no hub
    /// </summary>
    public async Task<T?> InvokeAsync<T>(string methodName, params object[] args)
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected to hub");

        return await _connection.InvokeAsync<T>(methodName, args);
    }

    /// <summary>
    /// Invoca um método no hub sem retorno
    /// </summary>
    public async Task InvokeAsync(string methodName, params object[] args)
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected to hub");

        await _connection.InvokeAsync(methodName, args);
    }

    /// <summary>
    /// Aguarda receber uma mensagem específica dentro de um timeout
    /// </summary>
    public async Task<bool> WaitForMessageAsync(Func<object, bool> predicate, TimeSpan timeout)
    {
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < timeout)
        {
            if (_receivedMessages.Any(predicate))
                return true;

            await Task.Delay(100);
        }

        return false;
    }

    /// <summary>
    /// Aguarda receber um contexto específico dentro de um timeout
    /// </summary>
    public async Task<bool> WaitForContextAsync(Func<object, bool> predicate, TimeSpan timeout)
    {
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < timeout)
        {
            if (_receivedContexts.Any(predicate))
                return true;

            await Task.Delay(100);
        }

        return false;
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

    /// <summary>
    /// Limpa mensagens e contextos recebidos
    /// </summary>
    public void ClearReceived()
    {
        _receivedMessages.Clear();
        _receivedContexts.Clear();
    }

    /// <summary>
    /// Converte um objeto recebido para um tipo específico
    /// </summary>
    public T? ConvertMessage<T>(object message)
    {
        if (message is JsonElement jsonElement)
        {
            return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
        }

        if (message is T directType)
        {
            return directType;
        }

        var json = JsonSerializer.Serialize(message);
        return JsonSerializer.Deserialize<T>(json);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await DisconnectAsync();
            _disposed = true;
        }
    }
}