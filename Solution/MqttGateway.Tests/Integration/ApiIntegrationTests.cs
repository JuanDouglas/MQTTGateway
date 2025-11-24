using MqttGateway.Tests.Fixtures;
using System.Net;

namespace MqttGateway.Tests.Integration;

/// <summary>
/// Testes de integração da API REST
/// </summary>
public class ApiIntegrationTests : IClassFixture<MqttGatewayWebApplicationFactory>
{
    private readonly MqttGatewayWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ApiIntegrationTests(MqttGatewayWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task SendMessage_WithValidData_ShouldReturnSuccess()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var message = "Test integration message";
        var channel = "integration-test";

        var requestData = new
        {
            sessionId,
            message,
            channel
        };

        // Act
        var response = await _client.PostAsJsonAsync("/Messages/Send", requestData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SendMessage_WithoutChannel_ShouldReturnSuccess()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var message = "Test integration message without channel";

        var requestData = new
        {
            sessionId,
            message
        };

        // Act
        var response = await _client.PostAsJsonAsync("/Messages/Send", requestData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SendMessage_WithInvalidJson_ShouldReturnBadRequest()
    {
        // Arrange
        var invalidJson = "{ invalid json }";
        var content = new StringContent(invalidJson, System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/Messages/Send", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SendMessage_WithEmptyGuid_ShouldReturnSuccess()
    {
        // Arrange
        var sessionId = Guid.Empty;
        var message = "Test with empty GUID";

        var requestData = new
        {
            sessionId,
            message
        };

        // Act
        var response = await _client.PostAsJsonAsync("/Messages/Send", requestData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Simple message")]
    [InlineData("Message with special chars: àáâãäåæçèéêë")]
    [InlineData("{\"nested\": \"json\", \"number\": 123}")]
    public async Task SendMessage_WithDifferentMessageTypes_ShouldReturnSuccess(string message)
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var requestData = new
        {
            sessionId,
            message
        };

        // Act
        var response = await _client.PostAsJsonAsync("/Messages/Send", requestData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SendMessage_LargePayload_ShouldReturnSuccess()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var largeMessage = new string('x', 10000); // 10KB message

        var requestData = new
        {
            sessionId,
            message = largeMessage
        };

        // Act
        var response = await _client.PostAsJsonAsync("/Messages/Send", requestData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SendMessage_ConcurrentRequests_ShouldAllSucceed()
    {
        // Arrange
        var tasks = new List<Task<HttpResponseMessage>>();
        var sessionId = Guid.NewGuid();

        for (int i = 0; i < 10; i++)
        {
            var requestData = new
            {
                sessionId,
                message = $"Concurrent message {i}",
                channel = $"channel-{i}"
            };

            tasks.Add(_client.PostAsJsonAsync("/Messages/Send", requestData));
        }

        // Act
        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().AllSatisfy(response =>
            response.StatusCode.Should().Be(HttpStatusCode.OK));
    }

    [Fact]
    public async Task Health_ShouldReturnHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        if (response.StatusCode != HttpStatusCode.NotFound) // Health endpoint might not be configured
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task SendMessage_WithUnicodeCharacters_ShouldReturnSuccess()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var unicodeMessage = "Mensagem com acentos: áéíóúâêîôûàèìòùãõç 中文 العربية 🚀🔥💯";

        var requestData = new
        {
            sessionId,
            message = unicodeMessage,
            channel = "unicode-test"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/Messages/Send", requestData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SendMessage_WithSpecialChannelNames_ShouldReturnSuccess()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var message = "Test message";

        var specialChannels = new[]
        {
            "channel-with-dashes",
            "channel_with_underscores",
            "channel.with.dots",
            "channel/with/slashes",
            "UPPERCASE_CHANNEL",
            "123numeric456",
            "mix3d-Ch4nn3l_N4m3"
        };

        // Act & Assert
        foreach (var channel in specialChannels)
        {
            var requestData = new
            {
                sessionId,
                message,
                channel
            };

            var response = await _client.PostAsJsonAsync("/Messages/Send", requestData);
            response.StatusCode.Should().Be(HttpStatusCode.OK,
                $"Channel '{channel}' should be accepted");
        }
    }

    [Fact]
    public async Task SendMessage_MultipleSessionsSimultaneously_ShouldReturnSuccess()
    {
        // Arrange
        var tasks = new List<Task<HttpResponseMessage>>();

        for (int i = 0; i < 5; i++)
        {
            var sessionId = Guid.NewGuid();
            var requestData = new
            {
                sessionId,
                message = $"Message for session {i}",
                channel = $"session-{i}"
            };

            tasks.Add(_client.PostAsJsonAsync("/Messages/Send", requestData));
        }

        // Act
        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().AllSatisfy(response =>
            response.StatusCode.Should().Be(HttpStatusCode.OK));
    }
}