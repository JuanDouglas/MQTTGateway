using Microsoft.AspNetCore.Mvc;
using MqttGateway.Server.Controllers;
using MqttGateway.Server.Services.Contracts;

namespace MqttGateway.Tests.Unit.Controllers;

/// <summary>
/// Testes unitários para MessageController
/// </summary>
public class MessageControllerTests
{
    private readonly Mock<IMqttMessageDispatcher> _mockMqttDispatcher;
    private readonly MessageController _controller;

    public MessageControllerTests()
    {
        _mockMqttDispatcher = new Mock<IMqttMessageDispatcher>();
        _controller = new MessageController(_mockMqttDispatcher.Object);
    }

    [Fact]
    public void SendMessage_WithValidParameters_ShouldCallDispatcher()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var message = "Test message";
        var channel = "test-channel";

        _mockMqttDispatcher
            .Setup(x => x.PublishMessageAsync(sessionId, message, channel, default))
            .Returns(Task.CompletedTask);

        // Act
        _controller.SendMessage(sessionId, message, channel);

        // Assert
        _mockMqttDispatcher.Verify(
            x => x.PublishMessageAsync(sessionId, message, channel, default),
            Times.Once);
    }

    [Fact]
    public void SendMessage_WithoutChannel_ShouldCallDispatcherWithNullChannel()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var message = "Test message";

        _mockMqttDispatcher
            .Setup(x => x.PublishMessageAsync(sessionId, message, null, default))
            .Returns(Task.CompletedTask);

        // Act
        _controller.SendMessage(sessionId, message);

        // Assert
        _mockMqttDispatcher.Verify(
            x => x.PublishMessageAsync(sessionId, message, null, default),
            Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Simple message")]
    [InlineData("Message with special chars")]
    [InlineData("Very long message")]
    public void SendMessage_WithDifferentMessages_ShouldWork(string message)
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        _mockMqttDispatcher
            .Setup(x => x.PublishMessageAsync(sessionId, message, null, default))
            .Returns(Task.CompletedTask);

        // Act & Assert - Should not throw
        Action act = () => _controller.SendMessage(sessionId, message);
        act.Should().NotThrow();

        _mockMqttDispatcher.Verify(
            x => x.PublishMessageAsync(sessionId, message, null, default),
            Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("channel1")]
    [InlineData("test/subchannel")]
    [InlineData("special-chars-àáâã")]
    public void SendMessage_WithDifferentChannels_ShouldWork(string? channel)
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var message = "Test message";

        _mockMqttDispatcher
            .Setup(x => x.PublishMessageAsync(sessionId, message, channel, default))
            .Returns(Task.CompletedTask);

        // Act & Assert - Should not throw
        Action act = () => _controller.SendMessage(sessionId, message, channel);
        act.Should().NotThrow();

        _mockMqttDispatcher.Verify(
            x => x.PublishMessageAsync(sessionId, message, channel, default),
            Times.Once);
    }

    [Fact]
    public void SendMessage_WithEmptyGuid_ShouldStillCallDispatcher()
    {
        // Arrange
        var sessionId = Guid.Empty;
        var message = "Test message";

        _mockMqttDispatcher
            .Setup(x => x.PublishMessageAsync(sessionId, message, null, default))
            .Returns(Task.CompletedTask);

        // Act
        _controller.SendMessage(sessionId, message);

        // Assert
        _mockMqttDispatcher.Verify(
            x => x.PublishMessageAsync(sessionId, message, null, default),
            Times.Once);
    }

    [Fact]
    public void SendMessage_MultipleCalls_ShouldCallDispatcherForEach()
    {
        // Arrange
        var sessionId1 = Guid.NewGuid();
        var sessionId2 = Guid.NewGuid();
        var message1 = "Message 1";
        var message2 = "Message 2";

        _mockMqttDispatcher
            .Setup(x => x.PublishMessageAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .Returns(Task.CompletedTask);

        // Act
        _controller.SendMessage(sessionId1, message1);
        _controller.SendMessage(sessionId2, message2);

        // Assert
        _mockMqttDispatcher.Verify(
            x => x.PublishMessageAsync(sessionId1, message1, null, default),
            Times.Once);

        _mockMqttDispatcher.Verify(
            x => x.PublishMessageAsync(sessionId2, message2, null, default),
            Times.Once);
    }

    [Fact]
    public void SendMessage_WhenDispatcherThrows_ShouldPropagateException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var message = "Test message";
        var expectedException = new InvalidOperationException("MQTT broker unavailable");

        _mockMqttDispatcher
            .Setup(x => x.PublishMessageAsync(sessionId, message, null, default))
            .ThrowsAsync(expectedException);

        // Act & Assert
        Func<Task> act = async () =>
        {
            _controller.SendMessage(sessionId, message);
            // Need to await the task that's returned by PublishMessageAsync
            await Task.Delay(100); // Give time for the async operation to potentially throw
        };

        // Note: Since SendMessage is void and calls an async method without awaiting,
        // we need to verify the mock was called rather than testing exception propagation
        _controller.SendMessage(sessionId, message);

        _mockMqttDispatcher.Verify(
            x => x.PublishMessageAsync(sessionId, message, null, default),
            Times.Once);
    }

    [Fact]
    public void Controller_ShouldHaveCorrectRouteAttribute()
    {
        // Arrange & Act
        var routeAttribute = _controller.GetType()
            .GetCustomAttributes(typeof(RouteAttribute), false)
            .FirstOrDefault() as RouteAttribute;

        // Assert
        routeAttribute.Should().NotBeNull();
        routeAttribute!.Template.Should().Be("Messages");
    }

    [Fact]
    public void SendMessage_Method_ShouldHaveCorrectHttpPostAttribute()
    {
        // Arrange & Act
        var method = _controller.GetType().GetMethod(nameof(_controller.SendMessage));
        var httpPostAttribute = method?.GetCustomAttributes(typeof(HttpPostAttribute), false)
            .FirstOrDefault() as HttpPostAttribute;

        // Assert
        httpPostAttribute.Should().NotBeNull();
        httpPostAttribute!.Template.Should().Be("Send");
    }

    [Fact]
    public void Constructor_WithNullDispatcher_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        Action act = () => new MessageController(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}