using Microsoft.AspNetCore.SignalR;
using MqttGateway.Server.Hubs;
using MqttGateway.Server.Objects;
using MqttGateway.Server.Services;
using MqttGateway.Server.Services.Contracts;

namespace MqttGateway.Tests.Unit.Services;

/// <summary>
/// Testes unitários para SignalRMessageRelay
/// </summary>
public class SignalRMessageRelayTests
{
    private readonly Mock<ISessionManager> _mockSessionManager;
    private readonly Mock<ISessionContextStore> _mockSessionContextStore;
    private readonly Mock<IHubContext<UserHub>> _mockHubContext;
    private readonly Mock<IHubClients> _mockClients;
    private readonly Mock<IClientProxy> _mockClientProxy;
    private readonly SignalRMessageRelay _messageRelay;

    public SignalRMessageRelayTests()
    {
        _mockSessionManager = new Mock<ISessionManager>();
        _mockSessionContextStore = new Mock<ISessionContextStore>();
        _mockHubContext = new Mock<IHubContext<UserHub>>();
        _mockClients = new Mock<IHubClients>();
        _mockClientProxy = new Mock<IClientProxy>();

        _mockHubContext.Setup(x => x.Clients).Returns(_mockClients.Object);

        _messageRelay = new SignalRMessageRelay(
            _mockSessionManager.Object,
            _mockSessionContextStore.Object,
            _mockHubContext.Object);
    }

    [Fact]
    public void DispatchEvent_NewSession_ShouldCreateContextAndBroadcast()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var payload = "Test message";
        var channel = "test-channel";
        var connectionIds = new HashSet<string> { "connection-1", "connection-2" };

        _mockSessionContextStore
            .Setup(x => x.GetContext(sessionId))
            .Returns((SessionContext?)null);

        _mockSessionContextStore
            .Setup(x => x.CreateContext(sessionId, payload))
            .Returns(true);

        _mockSessionManager
            .Setup(x => x.RelayClients(sessionId))
            .Returns(connectionIds);

        _mockClients
            .Setup(x => x.Groups(connectionIds))
            .Returns(_mockClientProxy.Object);

        _mockClientProxy
            .Setup(x => x.SendAsync("ReceiveMessage", It.IsAny<object>(), default))
            .Returns(Task.CompletedTask);

        // Act
        _messageRelay.DispatchEvent(sessionId, payload, channel);

        // Assert
        _mockSessionContextStore.Verify(
            x => x.CreateContext(sessionId, payload),
            Times.Once);

        _mockSessionManager.Verify(
            x => x.RelayClients(sessionId),
            Times.Once);

        _mockClientProxy.Verify(
            x => x.SendAsync("ReceiveMessage",
                It.IsAny<object>(),
                default),
            Times.Once);
    }

    [Fact]
    public void DispatchEvent_ExistingSession_ShouldUpdateContextAndBroadcast()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var payload = "Test message";
        var channel = "test-channel";
        var connectionIds = new HashSet<string> { "connection-1" };

        var mockContext = new Mock<SessionContext>("initial");

        _mockSessionContextStore
            .Setup(x => x.GetContext(sessionId))
            .Returns(mockContext.Object);

        _mockSessionManager
            .Setup(x => x.RelayClients(sessionId))
            .Returns(connectionIds);

        _mockClients
            .Setup(x => x.Groups(connectionIds))
            .Returns(_mockClientProxy.Object);

        _mockClientProxy
            .Setup(x => x.SendAsync("ReceiveMessage", It.IsAny<object>(), default))
            .Returns(Task.CompletedTask);

        // Act
        _messageRelay.DispatchEvent(sessionId, payload, channel);

        // Assert
        mockContext.Verify(
            x => x.IncressPayload(payload, channel),
            Times.Once);

        _mockSessionContextStore.Verify(
            x => x.CreateContext(It.IsAny<Guid>(), It.IsAny<string>()),
            Times.Never);

        _mockClientProxy.Verify(
            x => x.SendAsync("ReceiveMessage", It.IsAny<object>(), default),
            Times.Once);
    }

    [Fact]
    public void DispatchEvent_WithoutChannel_ShouldBroadcastWithNullChannel()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var payload = "Test message";
        var connectionIds = new HashSet<string> { "connection-1" };

        _mockSessionContextStore
            .Setup(x => x.GetContext(sessionId))
            .Returns((SessionContext?)null);

        _mockSessionContextStore
            .Setup(x => x.CreateContext(sessionId, payload))
            .Returns(true);

        _mockSessionManager
            .Setup(x => x.RelayClients(sessionId))
            .Returns(connectionIds);

        _mockClients
            .Setup(x => x.Groups(connectionIds))
            .Returns(_mockClientProxy.Object);

        _mockClientProxy
            .Setup(x => x.SendAsync("ReceiveMessage", It.IsAny<object>(), default))
            .Returns(Task.CompletedTask);

        // Act
        _messageRelay.DispatchEvent(sessionId, payload);

        // Assert
        _mockClientProxy.Verify(
            x => x.SendAsync("ReceiveMessage",
                It.IsAny<object>(),
                default),
            Times.Once);
    }

    [Fact]
    public void DispatchEvent_NoConnectedClients_ShouldStillUpdateContext()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var payload = "Test message";
        var channel = "test-channel";
        var connectionIds = new HashSet<string>(); // Empty set

        _mockSessionContextStore
            .Setup(x => x.GetContext(sessionId))
            .Returns((SessionContext?)null);

        _mockSessionContextStore
            .Setup(x => x.CreateContext(sessionId, payload))
            .Returns(true);

        _mockSessionManager
            .Setup(x => x.RelayClients(sessionId))
            .Returns(connectionIds);

        _mockClients
            .Setup(x => x.Groups(connectionIds))
            .Returns(_mockClientProxy.Object);

        _mockClientProxy
            .Setup(x => x.SendAsync("ReceiveMessage", It.IsAny<object>(), default))
            .Returns(Task.CompletedTask);

        // Act
        _messageRelay.DispatchEvent(sessionId, payload, channel);

        // Assert
        _mockSessionContextStore.Verify(
            x => x.CreateContext(sessionId, payload),
            Times.Once);

        // Should still try to broadcast even with no clients
        _mockClientProxy.Verify(
            x => x.SendAsync("ReceiveMessage", It.IsAny<object>(), default),
            Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Simple message")]
    [InlineData("Message with special chars")]
    [InlineData("JSON message")]
    [InlineData("Very long message")]
    public void DispatchEvent_WithDifferentPayloads_ShouldWork(string payload)
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var connectionIds = new HashSet<string> { "connection-1" };

        _mockSessionContextStore
            .Setup(x => x.GetContext(sessionId))
            .Returns((SessionContext?)null);

        _mockSessionContextStore
            .Setup(x => x.CreateContext(sessionId, payload))
            .Returns(true);

        _mockSessionManager
            .Setup(x => x.RelayClients(sessionId))
            .Returns(connectionIds);

        _mockClients
            .Setup(x => x.Groups(connectionIds))
            .Returns(_mockClientProxy.Object);

        _mockClientProxy
            .Setup(x => x.SendAsync("ReceiveMessage", It.IsAny<object>(), default))
            .Returns(Task.CompletedTask);

        // Act & Assert - Should not throw
        Action act = () => _messageRelay.DispatchEvent(sessionId, payload);
        act.Should().NotThrow();

        _mockClientProxy.Verify(
            x => x.SendAsync("ReceiveMessage",
                It.IsAny<object>(),
                default),
            Times.Once);
    }

    [Fact]
    public void DispatchEvent_MultipleConnectionsInSession_ShouldBroadcastToAll()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var payload = "Test message";
        var connectionIds = new HashSet<string> { "connection-1", "connection-2", "connection-3" };

        _mockSessionContextStore
            .Setup(x => x.GetContext(sessionId))
            .Returns((SessionContext?)null);

        _mockSessionContextStore
            .Setup(x => x.CreateContext(sessionId, payload))
            .Returns(true);

        _mockSessionManager
            .Setup(x => x.RelayClients(sessionId))
            .Returns(connectionIds);

        _mockClients
            .Setup(x => x.Groups(connectionIds))
            .Returns(_mockClientProxy.Object);

        _mockClientProxy
            .Setup(x => x.SendAsync("ReceiveMessage", It.IsAny<object>(), default))
            .Returns(Task.CompletedTask);

        // Act
        _messageRelay.DispatchEvent(sessionId, payload);

        // Assert
        _mockClients.Verify(
            x => x.Groups(connectionIds),
            Times.Once);

        _mockClientProxy.Verify(
            x => x.SendAsync("ReceiveMessage", It.IsAny<object>(), default),
            Times.Once);
    }

    [Fact]
    public async Task DispatchEvent_ConcurrentCalls_ShouldHandleCorrectly()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var connectionIds = new HashSet<string> { "connection-1" };
        var tasks = new List<Task>();

        _mockSessionContextStore
            .Setup(x => x.GetContext(sessionId))
            .Returns((SessionContext?)null);

        _mockSessionContextStore
            .Setup(x => x.CreateContext(sessionId, It.IsAny<string>()))
            .Returns(true);

        _mockSessionManager
            .Setup(x => x.RelayClients(sessionId))
            .Returns(connectionIds);

        _mockClients
            .Setup(x => x.Groups(connectionIds))
            .Returns(_mockClientProxy.Object);

        _mockClientProxy
            .Setup(x => x.SendAsync("ReceiveMessage", It.IsAny<object>(), default))
            .Returns(Task.CompletedTask);

        // Act - Concurrent dispatch calls
        for (int i = 0; i < 10; i++)
        {
            var payload = $"Message {i}";
            tasks.Add(Task.Run(() => _messageRelay.DispatchEvent(sessionId, payload)));
        }

        // Assert - Should not throw
        var aggregateTask = Task.WhenAll(tasks);
        await aggregateTask; // Should complete without throwing
    }
}