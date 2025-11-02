using Microsoft.AspNetCore.Mvc;
using MqttGateway.Server.Services.Contracts;
using System.ComponentModel.DataAnnotations;

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

        await _mqttDispatcher.PublishMessageAsync(@new.SessionId, @new.Message, @new.Channel);

        return Ok();
    }
}

public record NewMeessage
{
    public Guid SessionId { get; set; }
    public required string Message { get; set; }
    public string? Channel { get; set; }
}