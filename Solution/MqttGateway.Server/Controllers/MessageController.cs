using Microsoft.AspNetCore.Mvc;
using MqttGateway.Server.Services.Contracts;

namespace MqttGateway.Server.Controllers;

[Route("Messages")]
public class MessageController : ControllerBase
{
    private readonly IMqttMessageDispatcher _mqttDispatcher;

    public MessageController(
        IMqttMessageDispatcher mqttMessageDispatcher)
    {
        _mqttDispatcher = mqttMessageDispatcher;
    }

    [HttpPost("Send")]
    public async Task<IActionResult> SendMessage([FromBody] NewMeessage @new)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (@new.TargetId.HasValue)
            await _mqttDispatcher.PublishMessageAsync(@new.SessionId, @new.Message, @new.Channel);
        else
            await _mqttDispatcher.PublishDirectMessageAsync(@new.SessionId, @new.TargetId!.Value, @new.Message, @new.Channel);

        return NoContent();
    }
}

public record NewMeessage
{
    public Guid SessionId { get; set; }
    public required string Message { get; set; }
    public string? Channel { get; set; }
    public Guid? TargetId { get; set; }
}