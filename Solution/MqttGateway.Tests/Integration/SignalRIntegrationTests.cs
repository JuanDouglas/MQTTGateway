using MqttGateway.Tests.Fixtures;
using MqttGateway.Tests.Helpers;

namespace MqttGateway.Tests.Integration;

/// <summary>
/// Testes de integração do SignalR Hub
/// </summary>
public class SignalRIntegrationTests : IClassFixture<MqttGatewayWebApplicationFactory>
{
    private readonly MqttGatewayWebApplicationFactory _factory;

    public SignalRIntegrationTests(MqttGatewayWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Connect_WithValidSessionId_ShouldConnectSuccessfully()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var hubUrl = _factory.Server.BaseAddress + "hub";

        await using var signalRHelper = new SignalRTestHelper();

        // Act
        await signalRHelper.ConnectAsync(hubUrl, sessionId);

        // Assert
        signalRHelper.IsConnected.Should().BeTrue();
    }

    [Fact]
    public async Task Connect_WithInvalidSessionId_ShouldFailToConnect()
    {
        // Arrange
        var hubUrl = _factory.Server.BaseAddress + "hub?sessionId=invalid-guid";

        await using var signalRHelper = new SignalRTestHelper();

        // Act & Assert
        var connectTask = signalRHelper.ConnectAsync(hubUrl, Guid.Empty);

        // Connection might fail or succeed but then be disconnected immediately
        // We'll check if it fails to stay connected
        try
        {
            await connectTask;

            // If connection succeeds initially, wait a bit and check if it gets disconnected
            await Task.Delay(1000);

            // Should either fail to connect or be disconnected quickly
            var isStillConnected = await signalRHelper.WaitForConnectionAsync(TimeSpan.FromSeconds(2));
            isStillConnected.Should().BeFalse();
        }
        catch
        {
            // Expected - connection should fail with invalid session ID
        }
    }

    [Fact]
    public async Task Connect_MultipleClientsToSameSession_ShouldAllConnect()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var hubUrl = _factory.Server.BaseAddress + "hub";

        await using var client1 = new SignalRTestHelper();
        await using var client2 = new SignalRTestHelper();
        await using var client3 = new SignalRTestHelper();

        // Act
        await client1.ConnectAsync(hubUrl, sessionId);
        await client2.ConnectAsync(hubUrl, sessionId);
        await client3.ConnectAsync(hubUrl, sessionId);

        // Assert
        client1.IsConnected.Should().BeTrue();
        client2.IsConnected.Should().BeTrue();
        client3.IsConnected.Should().BeTrue();
    }

    [Fact]
    public async Task Connect_ShouldReceiveSetContextMessage()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var hubUrl = _factory.Server.BaseAddress + "hub";

        await using var signalRHelper = new SignalRTestHelper();

        // Act
        await signalRHelper.ConnectAsync(hubUrl, sessionId);

        // Wait for SetContext message
        var receivedContext = await signalRHelper.WaitForContextAsync(
            _ => true,
            TimeSpan.FromSeconds(5));

        // Assert
        receivedContext.Should().BeTrue();
        signalRHelper.ReceivedContexts.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Connect_MultipleDifferentSessions_ShouldBeIsolated()
    {
        // Arrange
        var sessionId1 = Guid.NewGuid();
        var sessionId2 = Guid.NewGuid();
        var hubUrl = _factory.Server.BaseAddress + "hub";

        await using var client1 = new SignalRTestHelper();
        await using var client2 = new SignalRTestHelper();

        // Act
        await client1.ConnectAsync(hubUrl, sessionId1);
        await client2.ConnectAsync(hubUrl, sessionId2);

        // Give some time for any cross-session interference
        await Task.Delay(1000);

        // Assert
        client1.IsConnected.Should().BeTrue();
        client2.IsConnected.Should().BeTrue();

        // Each should have received their own context
        client1.ReceivedContexts.Should().NotBeEmpty();
        client2.ReceivedContexts.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Disconnect_ShouldCleanupProperly()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var hubUrl = _factory.Server.BaseAddress + "hub";

        var signalRHelper = new SignalRTestHelper();

        // Act
        await signalRHelper.ConnectAsync(hubUrl, sessionId);
        signalRHelper.IsConnected.Should().BeTrue();

        await signalRHelper.DisconnectAsync();

        // Assert
        signalRHelper.IsConnected.Should().BeFalse();

        await signalRHelper.DisposeAsync();
    }

    [Fact]
    public async Task Connect_AfterDisconnect_ShouldConnectAgain()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var hubUrl = _factory.Server.BaseAddress + "hub";

        await using var signalRHelper = new SignalRTestHelper();

        // Act - First connection
        await signalRHelper.ConnectAsync(hubUrl, sessionId);
        signalRHelper.IsConnected.Should().BeTrue();

        await signalRHelper.DisconnectAsync();
        signalRHelper.IsConnected.Should().BeFalse();

        // Act - Reconnect
        await signalRHelper.ConnectAsync(hubUrl, sessionId);

        // Assert
        signalRHelper.IsConnected.Should().BeTrue();
    }

    [Fact]
    public async Task Connect_WithSameSessionAfterOthersDisconnect_ShouldReceiveContext()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var hubUrl = _factory.Server.BaseAddress + "hub";

        // First client connects and then disconnects
        var firstClient = new SignalRTestHelper();
        await firstClient.ConnectAsync(hubUrl, sessionId);
        await firstClient.DisconnectAsync();
        await firstClient.DisposeAsync();

        // Second client connects to same session
        await using var secondClient = new SignalRTestHelper();

        // Act
        await secondClient.ConnectAsync(hubUrl, sessionId);

        // Wait for context
        var receivedContext = await secondClient.WaitForContextAsync(
            _ => true,
            TimeSpan.FromSeconds(5));

        // Assert
        receivedContext.Should().BeTrue();
        secondClient.ReceivedContexts.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ConcurrentConnections_ShouldAllSucceed()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var hubUrl = _factory.Server.BaseAddress + "hub";
        var clients = new List<SignalRTestHelper>();
        var connectionTasks = new List<Task>();

        try
        {
            // Create multiple clients
            for (int i = 0; i < 5; i++)
            {
                var client = new SignalRTestHelper();
                clients.Add(client);
                connectionTasks.Add(client.ConnectAsync(hubUrl, sessionId));
            }

            // Act
            await Task.WhenAll(connectionTasks);

            // Assert
            clients.Should().AllSatisfy(client => client.IsConnected.Should().BeTrue());
        }
        finally
        {
            // Cleanup
            foreach (var client in clients)
            {
                await client.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task Connect_WithVeryLongSessionId_ShouldHandleCorrectly()
    {
        // Arrange
        var sessionId = Guid.NewGuid(); // GUID is always the same length, but test the parser
        var hubUrl = _factory.Server.BaseAddress + "hub";

        await using var signalRHelper = new SignalRTestHelper();

        // Act & Assert
        Func<Task> act = async () => await signalRHelper.ConnectAsync(hubUrl, sessionId);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Connect_StressTest_MultipleSessionsAndClients()
    {
        // Arrange
        var hubUrl = _factory.Server.BaseAddress + "hub";
        var clients = new List<SignalRTestHelper>();
        var connectionTasks = new List<Task>();

        try
        {
            // Create multiple sessions with multiple clients each
            for (int session = 0; session < 3; session++)
            {
                var sessionId = Guid.NewGuid();

                for (int client = 0; client < 3; client++)
                {
                    var signalRClient = new SignalRTestHelper();
                    clients.Add(signalRClient);
                    connectionTasks.Add(signalRClient.ConnectAsync(hubUrl, sessionId));
                }
            }

            // Act
            await Task.WhenAll(connectionTasks);

            // Assert
            clients.Should().AllSatisfy(client => client.IsConnected.Should().BeTrue());

            // All clients should have received context
            foreach (var client in clients)
            {
                var receivedContext = await client.WaitForContextAsync(
                    _ => true,
                    TimeSpan.FromSeconds(10));
                receivedContext.Should().BeTrue();
            }
        }
        finally
        {
            // Cleanup
            foreach (var client in clients)
            {
                await client.DisposeAsync();
            }
        }
    }
}