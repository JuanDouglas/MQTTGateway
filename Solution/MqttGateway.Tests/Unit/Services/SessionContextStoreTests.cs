using MqttGateway.Server.Services;

namespace MqttGateway.Tests.Unit.Services;

/// <summary>
/// Testes unitários para SessionContextStore
/// </summary>
public class SessionContextStoreTests
{
    private readonly SessionContextStore _sessionContextStore;

    public SessionContextStoreTests()
    {
        _sessionContextStore = new SessionContextStore();
    }

    [Fact]
    public void CreateContext_WhenSessionDoesNotExist_ShouldReturnTrue()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var startMessage = "Initial message";

        // Act
        var result = _sessionContextStore.CreateContext(sessionId, startMessage);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CreateContext_WhenSessionAlreadyExists_ShouldReturnFalse()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var startMessage = "Initial message";
        _sessionContextStore.CreateContext(sessionId, startMessage);

        // Act
        var result = _sessionContextStore.CreateContext(sessionId, "Another message");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetContext_WhenSessionExists_ShouldReturnContext()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var startMessage = "Initial message";
        _sessionContextStore.CreateContext(sessionId, startMessage);

        // Act
        var context = _sessionContextStore.GetContext(sessionId);

        // Assert
        context.Should().NotBeNull();
    }

    [Fact]
    public void GetContext_WhenSessionDoesNotExist_ShouldReturnNull()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var context = _sessionContextStore.GetContext(sessionId);

        // Assert
        context.Should().BeNull();
    }

    [Fact]
    public void RemoveContext_WhenSessionExists_ShouldReturnTrue()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var startMessage = "Initial message";
        _sessionContextStore.CreateContext(sessionId, startMessage);

        // Act
        var result = _sessionContextStore.RemoveContext(sessionId);

        // Assert
        result.Should().BeTrue();
        _sessionContextStore.GetContext(sessionId).Should().BeNull();
    }

    [Fact]
    public void RemoveContext_WhenSessionDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var result = _sessionContextStore.RemoveContext(sessionId);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Test message")]
    [InlineData("Message with special chars")]
    [InlineData("Very long message")]
    public void CreateContext_WithDifferentStartMessages_ShouldWork(string startMessage)
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var result = _sessionContextStore.CreateContext(sessionId, startMessage);

        // Assert
        result.Should().BeTrue();
        var context = _sessionContextStore.GetContext(sessionId);
        context.Should().NotBeNull();
    }

    [Fact]
    public void MultipleOperations_ShouldWorkCorrectly()
    {
        // Arrange
        var sessionIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();

        // Act & Assert - Create multiple contexts
        foreach (var sessionId in sessionIds)
        {
            var result = _sessionContextStore.CreateContext(sessionId, $"Message for {sessionId}");
            result.Should().BeTrue();
        }

        // Assert - All contexts should exist
        foreach (var sessionId in sessionIds)
        {
            _sessionContextStore.GetContext(sessionId).Should().NotBeNull();
        }

        // Act - Remove some contexts
        var toRemove = sessionIds.Take(3).ToList();
        foreach (var sessionId in toRemove)
        {
            var result = _sessionContextStore.RemoveContext(sessionId);
            result.Should().BeTrue();
        }

        // Assert - Removed contexts should not exist, others should still exist
        foreach (var sessionId in toRemove)
        {
            _sessionContextStore.GetContext(sessionId).Should().BeNull();
        }

        foreach (var sessionId in sessionIds.Skip(3))
        {
            _sessionContextStore.GetContext(sessionId).Should().NotBeNull();
        }
    }

    [Fact]
    public async Task ThreadSafety_ConcurrentOperations_ShouldNotThrow()
    {
        // Arrange
        var sessionIds = Enumerable.Range(0, 100).Select(_ => Guid.NewGuid()).ToList();
        var tasks = new List<Task>();

        // Act - Concurrent operations
        foreach (var sessionId in sessionIds)
        {
            tasks.Add(Task.Run(() => _sessionContextStore.CreateContext(sessionId, $"Message for {sessionId}")));
        }

        // Assert - Should not throw
        var aggregateTask = Task.WhenAll(tasks);
        await aggregateTask; // Should complete without throwing
    }
}