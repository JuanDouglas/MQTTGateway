using MqttGateway.Tests.Fixtures;
using MqttGateway.Tests.Helpers;
using System.Diagnostics;
using Xunit.Abstractions;

namespace MqttGateway.Tests.Performance;

/// <summary>
/// Testes de performance para identificar gargalos e validar SLAs
/// </summary>
public class PerformanceTests : IClassFixture<MqttGatewayWebApplicationFactory>
{
    private readonly MqttGatewayWebApplicationFactory _factory;
    private readonly HttpClient _httpClient;
    private readonly ITestOutputHelper _output;

    public PerformanceTests(MqttGatewayWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _httpClient = _factory.CreateClient();
        _output = output;
    }

    [Fact]
    public async Task ApiResponse_SingleRequest_ShouldBeFast()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var message = "Performance test message";
        var requestData = new { sessionId, message };

        // Act
        var stopwatch = Stopwatch.StartNew();
        var response = await _httpClient.PostAsJsonAsync("/Messages/Send", requestData);
        stopwatch.Stop();

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, "Single API request should complete in under 100ms");

        _output.WriteLine($"Single request took: {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task ApiThroughput_ConcurrentRequests_ShouldMeetTargets()
    {
        // Arrange
        const int concurrentRequests = 100;
        const int maxAcceptableTimeMs = 5000; // 5 seconds for 100 requests

        var sessionId = Guid.NewGuid();
        var tasks = new List<Task<HttpResponseMessage>>();

        // Act
        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < concurrentRequests; i++)
        {
            var requestData = new { sessionId, message = $"Concurrent message {i}" };
            tasks.Add(_httpClient.PostAsJsonAsync("/Messages/Send", requestData));
        }

        var responses = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        responses.Should().AllSatisfy(r => r.IsSuccessStatusCode.Should().BeTrue());
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxAcceptableTimeMs,
            $"100 concurrent requests should complete in under {maxAcceptableTimeMs}ms");

        var throughput = (double)concurrentRequests / stopwatch.Elapsed.TotalSeconds;
        throughput.Should().BeGreaterThan(20, "Should handle at least 20 requests per second");

        _output.WriteLine($"100 concurrent requests took: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Throughput: {throughput:F2} requests/second");
    }

    [Fact]
    public async Task SignalRConnection_MultipleClients_ShouldConnectQuickly()
    {
        // Arrange
        const int clientCount = 50;
        const int maxConnectionTimeMs = 10000; // 10 seconds for 50 connections

        var sessionId = Guid.NewGuid();
        var hubUrl = _factory.Server.BaseAddress + "hub";
        var clients = new List<SignalRTestHelper>();
        var connectionTasks = new List<Task>();

        try
        {
            // Act
            var stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < clientCount; i++)
            {
                var client = new SignalRTestHelper();
                clients.Add(client);
                connectionTasks.Add(client.ConnectAsync(hubUrl, sessionId));
            }

            await Task.WhenAll(connectionTasks);
            stopwatch.Stop();

            // Assert
            clients.Should().AllSatisfy(c => c.IsConnected.Should().BeTrue());
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxConnectionTimeMs,
                $"{clientCount} SignalR connections should establish in under {maxConnectionTimeMs}ms");

            var connectionsPerSecond = (double)clientCount / stopwatch.Elapsed.TotalSeconds;

            _output.WriteLine($"{clientCount} SignalR connections took: {stopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"Connection rate: {connectionsPerSecond:F2} connections/second");
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
    public async Task MessageLatency_ApiToSignalR_ShouldBeLow()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var hubUrl = _factory.Server.BaseAddress + "hub";
        var message = "Latency test message";

        await using var signalRClient = new SignalRTestHelper();

        // Setup
        await signalRClient.ConnectAsync(hubUrl, sessionId);
        await signalRClient.WaitForContextAsync(_ => true, TimeSpan.FromSeconds(5));
        signalRClient.ClearReceived();

        // Act
        var sendTime = DateTime.UtcNow;
        var requestData = new { sessionId, message };

        var sendTask = _httpClient.PostAsJsonAsync("/Messages/Send", requestData);
        var receiveTask = signalRClient.WaitForMessageAsync(
            msg => signalRClient.ConvertMessage<dynamic>(msg)?.Payload?.ToString() == message,
            TimeSpan.FromSeconds(5));

        await Task.WhenAll(sendTask, receiveTask);
        var receiveTime = DateTime.UtcNow;

        var latency = receiveTime - sendTime;

        // Assert
        receiveTask.Result.Should().BeTrue("Message should be received");
        latency.TotalMilliseconds.Should().BeLessThan(1000, "End-to-end latency should be under 1 second");

        _output.WriteLine($"End-to-end latency: {latency.TotalMilliseconds:F2}ms");
    }

    [Fact]
    public async Task MemoryUsage_ManyConnections_ShouldNotLeak()
    {
        // Arrange
        const int connectionCycles = 10;
        const int connectionsPerCycle = 20;

        var sessionId = Guid.NewGuid();
        var hubUrl = _factory.Server.BaseAddress + "hub";

        var initialMemory = GC.GetTotalMemory(true);

        // Act - Multiple cycles of connect/disconnect
        for (int cycle = 0; cycle < connectionCycles; cycle++)
        {
            var clients = new List<SignalRTestHelper>();

            try
            {
                // Connect many clients
                for (int i = 0; i < connectionsPerCycle; i++)
                {
                    var client = new SignalRTestHelper();
                    clients.Add(client);
                    await client.ConnectAsync(hubUrl, sessionId);
                }

                // Wait a bit
                await Task.Delay(100);

                // Disconnect all
                foreach (var client in clients)
                {
                    await client.DisconnectAsync();
                }
            }
            finally
            {
                foreach (var client in clients)
                {
                    await client.DisposeAsync();
                }
            }

            // Force garbage collection after each cycle
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        var finalMemory = GC.GetTotalMemory(true);
        var memoryIncrease = finalMemory - initialMemory;

        // Assert
        // Allow some memory increase but not excessive
        memoryIncrease.Should().BeLessThan(50 * 1024 * 1024, "Memory increase should be less than 50MB");

        _output.WriteLine($"Initial memory: {initialMemory / 1024 / 1024:F2} MB");
        _output.WriteLine($"Final memory: {finalMemory / 1024 / 1024:F2} MB");
        _output.WriteLine($"Memory increase: {memoryIncrease / 1024 / 1024:F2} MB");
    }

    [Fact]
    public async Task Stress_HighVolumeMessages_ShouldMaintainPerformance()
    {
        // Arrange
        const int messageCount = 1000;
        const int maxTotalTimeMs = 30000; // 30 seconds for 1000 messages

        var sessionId = Guid.NewGuid();
        var hubUrl = _factory.Server.BaseAddress + "hub";

        await using var signalRClient = new SignalRTestHelper();

        // Setup
        await signalRClient.ConnectAsync(hubUrl, sessionId);
        await signalRClient.WaitForContextAsync(_ => true, TimeSpan.FromSeconds(5));

        // Act
        var stopwatch = Stopwatch.StartNew();
        var sendTasks = new List<Task>();

        for (int i = 0; i < messageCount; i++)
        {
            var requestData = new { sessionId, message = $"Stress test message {i}" };
            sendTasks.Add(_httpClient.PostAsJsonAsync("/Messages/Send", requestData));

            // Add small delay to avoid overwhelming the system
            if (i % 50 == 0)
            {
                await Task.Delay(10);
            }
        }

        await Task.WhenAll(sendTasks);
        stopwatch.Stop();

        // Wait for messages to be processed
        var startWait = DateTime.UtcNow;
        while (signalRClient.ReceivedMessages.Count < messageCount &&
               DateTime.UtcNow - startWait < TimeSpan.FromSeconds(30))
        {
            await Task.Delay(100);
        }

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxTotalTimeMs,
            $"Sending {messageCount} messages should complete in under {maxTotalTimeMs}ms");

        var throughput = (double)messageCount / stopwatch.Elapsed.TotalSeconds;
        throughput.Should().BeGreaterThan(30, "Should maintain at least 30 messages per second under load");

        // Most messages should arrive (allow for some loss under stress)
        var receivedPercentage = (double)signalRClient.ReceivedMessages.Count / messageCount * 100;
        receivedPercentage.Should().BeGreaterThan(95, "Should receive at least 95% of messages");

        _output.WriteLine($"Sent {messageCount} messages in: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Throughput: {throughput:F2} messages/second");
        _output.WriteLine($"Received: {signalRClient.ReceivedMessages.Count}/{messageCount} ({receivedPercentage:F1}%)");
    }

    [Fact]
    public async Task ResourceUtilization_UnderLoad_ShouldBeEfficient()
    {
        // Arrange
        const int simultaneousSessions = 10;
        const int clientsPerSession = 5;
        const int messagesPerSession = 20;

        var sessions = Enumerable.Range(0, simultaneousSessions)
            .Select(_ => Guid.NewGuid())
            .ToList();

        var hubUrl = _factory.Server.BaseAddress + "hub";
        var allClients = new List<SignalRTestHelper>();

        try
        {
            var stopwatch = Stopwatch.StartNew();

            // Phase 1: Connect all clients
            var connectionTasks = new List<Task>();
            foreach (var sessionId in sessions)
            {
                for (int c = 0; c < clientsPerSession; c++)
                {
                    var client = new SignalRTestHelper();
                    allClients.Add(client);
                    connectionTasks.Add(client.ConnectAsync(hubUrl, sessionId));
                }
            }

            await Task.WhenAll(connectionTasks);
            var connectionTime = stopwatch.ElapsedMilliseconds;

            // Phase 2: Send messages to all sessions
            var sendTasks = new List<Task>();
            foreach (var sessionId in sessions)
            {
                for (int m = 0; m < messagesPerSession; m++)
                {
                    var requestData = new { sessionId, message = $"Load test message {m}" };
                    sendTasks.Add(_httpClient.PostAsJsonAsync("/Messages/Send", requestData));
                }
            }

            await Task.WhenAll(sendTasks);
            stopwatch.Stop();

            var totalTime = stopwatch.ElapsedMilliseconds;
            var totalMessages = simultaneousSessions * messagesPerSession;
            var totalConnections = simultaneousSessions * clientsPerSession;

            // Assert
            allClients.Should().AllSatisfy(c => c.IsConnected.Should().BeTrue());

            var connectionRate = (double)totalConnections / (connectionTime / 1000.0);
            var messageRate = (double)totalMessages / (totalTime / 1000.0);

            connectionRate.Should().BeGreaterThan(10, "Should establish at least 10 connections per second");
            messageRate.Should().BeGreaterThan(5, "Should process at least 5 messages per second under load");

            _output.WriteLine($"Connected {totalConnections} clients in {connectionTime}ms ({connectionRate:F2} conn/sec)");
            _output.WriteLine($"Processed {totalMessages} messages in {totalTime}ms ({messageRate:F2} msg/sec)");
            _output.WriteLine($"Sessions: {simultaneousSessions}, Clients per session: {clientsPerSession}");
        }
        finally
        {
            // Cleanup
            foreach (var client in allClients)
            {
                await client.DisposeAsync();
            }
        }
    }
}