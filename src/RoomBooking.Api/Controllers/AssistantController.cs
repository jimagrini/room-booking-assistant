using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomBooking.Api.Assistant;

namespace RoomBooking.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/assistant")]
public sealed class AssistantController(
    IAssistantService assistantService)
    : ControllerBase
{
    [HttpPost("messages")]
    [ProducesResponseType<AssistantMessageResponse>(
        StatusCodes.Status200OK)]
    public async Task<ActionResult<AssistantMessageResponse>>
        SendMessage(
            AssistantMessageRequest request,
            CancellationToken cancellationToken)
    {
        var response = await assistantService.ReplyAsync(
            request.Message,
            request.ConversationId,
            cancellationToken);
        return Ok(response);
    }
}
