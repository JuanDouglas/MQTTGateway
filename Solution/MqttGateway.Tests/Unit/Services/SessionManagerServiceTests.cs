using MqttGateway.Server.Services;
using MqttGateway.Server.Services.Contracts;

namespace MqttGateway.Tests.Unit.Services;

/// <summary>
/// Testes unitários para SessionManagerService
/// </summary>
public class SessionManagerServiceTests
{
    private readonly Mock<IMqttBrokerConnectionHandler> _mockMqttConnectionHandler;
    private readonly Mock<ISessionContextStore> _mockSessionContextStore;
    private readonly SessionManagerService _sessionManager;

    public SessionManagerServiceTests()
    {
        _mockMqttConnectionHandler = new Mock<IMqttBrokerConnectionHandler>();
        _mockSessionContextStore = new Mock<ISessionContextStore>();

        _sessionManager = new SessionManagerService(
            _mockMqttConnectionHandler.Object,
            _mockSessionContextStore.Object);
    }

    [Fact]
    public async Task SubscribeContext_NewSession_ShouldCreateSessionAndSubscribeToMqtt()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var connectionId = "connection-1";

        _mockMqttConnectionHandler
            .Setup(x => x.SubscribeClientAsync(It.IsAny<Guid>(), sessionId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sessionManager.SubscribeContext(sessionId, connectionId);

        // Assert
        result.Should().BeTrue();

        _mockMqttConnectionHandler.Verify(
            x => x.SubscribeClientAsync(It.IsAny<Guid>(), sessionId, It.IsAny<CancellationToken>()),
            Times.Once);

        var relayClients = _sessionManager.RelayClients(sessionId);
        relayClients.Should().Contain(connectionId);
    }

    [Fact]
    public async Task SubscribeContext_ExistingSession_ShouldNotSubscribeToMqttAgain()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var connectionId1 = "connection-1";
        var connectionId2 = "connection-2";

        _mockMqttConnectionHandler
            .Setup(x => x.SubscribeClientAsync(It.IsAny<Guid>(), sessionId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sessionManager.SubscribeContext(sessionId, connectionId1);
        await _sessionManager.SubscribeContext(sessionId, connectionId2);

        // Assert
        _mockMqttConnectionHandler.Verify(
            x => x.SubscribeClientAsync(It.IsAny<Guid>(), sessionId, It.IsAny<CancellationToken>()),
            Times.Once); // Should only be called once

        var relayClients = _sessionManager.RelayClients(sessionId);
        relayClients.Should().Contain(connectionId1);
        relayClients.Should().Contain(connectionId2);
        relayClients.Should().HaveCount(2);
    }

    [Fact]
    public async Task SubscribeContext_SameConnectionTwice_ShouldReturnFalseSecondTime()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var connectionId = "connection-1";

        _mockMqttConnectionHandler
            .Setup(x => x.SubscribeClientAsync(It.IsAny<Guid>(), sessionId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result1 = await _sessionManager.SubscribeContext(sessionId, connectionId);
        var result2 = await _sessionManager.SubscribeContext(sessionId, connectionId);

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeFalse(); // HashSet.Add returns false for duplicates
    }

    [Fact]
    public async Task RemoveConnectionAsync_LastConnection_ShouldUnsubscribeAndRemoveSession()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var connectionId = "connection-1";

        _mockMqttConnectionHandler
            .Setup(x => x.SubscribeClientAsync(It.IsAny<Guid>(), sessionId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockMqttConnectionHandler
            .Setup(x => x.UnsubscribeClientAsync(sessionId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockSessionContextStore
            .Setup(x => x.RemoveContext(sessionId))
            .Returns(true);

        // Subscribe first
        await _sessionManager.SubscribeContext(sessionId, connectionId);

        // Act
        var result = await _sessionManager.RemoveConnectionAsync(sessionId, connectionId);

        // Assert
        result.Should().BeTrue();

        _mockMqttConnectionHandler.Verify(
            x => x.UnsubscribeClientAsync(sessionId, It.IsAny<CancellationToken>()),
            Times.Once);

        _mockSessionContextStore.Verify(
            x => x.RemoveContext(sessionId),
            Times.Once);

        var relayClients = _sessionManager.RelayClients(sessionId);
        relayClients.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveConnectionAsync_NotLastConnection_ShouldNotUnsubscribe()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var connectionId1 = "connection-1";
        var connectionId2 = "connection-2";

        _mockMqttConnectionHandler
            .Setup(x => x.SubscribeClientAsync(It.IsAny<Guid>(), sessionId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Subscribe both connections
        await _sessionManager.SubscribeContext(sessionId, connectionId1);
        await _sessionManager.SubscribeContext(sessionId, connectionId2);

        // Act
        var result = await _sessionManager.RemoveConnectionAsync(sessionId, connectionId1);

        // Assert
        result.Should().BeTrue();

        _mockMqttConnectionHandler.Verify(
            x => x.UnsubscribeClientAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _mockSessionContextStore.Verify(
            x => x.RemoveContext(It.IsAny<Guid>()),
            Times.Never);

        var relayClients = _sessionManager.RelayClients(sessionId);
        relayClients.Should().Contain(connectionId2);
        relayClients.Should().NotContain(connectionId1);
        relayClients.Should().HaveCount(1);
    }

    [Fact]
    public async Task RemoveConnectionAsync_NonExistentSession_ShouldReturnFalse()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var connectionId = "connection-1";

        // Act
        var result = await _sessionManager.RemoveConnectionAsync(sessionId, connectionId);

        // Assert
        result.Should().BeFalse();

        _mockMqttConnectionHandler.Verify(
            x => x.UnsubscribeClientAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RemoveConnectionAsync_NonExistentConnection_ShouldReturnFalse()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var connectionId1 = "connection-1";
        var connectionId2 = "connection-2";

        _mockMqttConnectionHandler
            .Setup(x => x.SubscribeClientAsync(It.IsAny<Guid>(), sessionId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Subscribe only one connection
        await _sessionManager.SubscribeContext(sessionId, connectionId1);

        // Act - Try to remove different connection
        var result = await _sessionManager.RemoveConnectionAsync(sessionId, connectionId2);

        // Assert
        result.Should().BeFalse();

        var relayClients = _sessionManager.RelayClients(sessionId);
        relayClients.Should().Contain(connectionId1);
        relayClients.Should().HaveCount(1);
    }

    [Fact]
    public void RelayClients_NonExistentSession_ShouldReturnEmptySet()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var relayClients = _sessionManager.RelayClients(sessionId);

        // Assert
        relayClients.Should().BeEmpty();
    }

    [Fact]
    public async Task MultipleSessions_ShouldBeIndependent()
    {
        // Arrange
        var sessionId1 = Guid.NewGuid();
        var sessionId2 = Guid.NewGuid();
        var connectionId1 = "connection-1";
        var connectionId2 = "connection-2";

        _mockMqttConnectionHandler
            .Setup(x => x.SubscribeClientAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sessionManager.SubscribeContext(sessionId1, connectionId1);
        await _sessionManager.SubscribeContext(sessionId2, connectionId2);

        // Assert
        var relayClients1 = _sessionManager.RelayClients(sessionId1);
        var relayClients2 = _sessionManager.RelayClients(sessionId2);

        relayClients1.Should().Contain(connectionId1);
        relayClients1.Should().NotContain(connectionId2);
        relayClients1.Should().HaveCount(1);

        relayClients2.Should().Contain(connectionId2);
        relayClients2.Should().NotContain(connectionId1);
        relayClients2.Should().HaveCount(1);
    }

    [Fact]
    public async Task CancellationToken_ShouldBePassedToMqttHandler()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var connectionId = "connection-1";
        var cancellationToken = new CancellationToken();

        _mockMqttConnectionHandler
            .Setup(x => x.SubscribeClientAsync(It.IsAny<Guid>(), sessionId, cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        await _sessionManager.SubscribeContext(sessionId, connectionId, cancellationToken);

        // Assert
        _mockMqttConnectionHandler.Verify(
            x => x.SubscribeClientAsync(It.IsAny<Guid>(), sessionId, cancellationToken),
            Times.Once);
    }
}