using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomBooking.Application.Bookings;

namespace RoomBooking.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/rooms")]
public sealed class RoomsController(
    IBookingService bookingService)
    : ControllerBase
{
    [HttpGet("available")]
    [ProducesResponseType<IReadOnlyList<RoomDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RoomDto>>> ListAvailable(
        [FromQuery] DateTimeOffset startTime,
        [FromQuery] DateTimeOffset endTime,
        [FromQuery] int attendeeCount,
        CancellationToken cancellationToken)
    {
        var rooms = await bookingService.ListAvailableRoomsAsync(
            startTime,
            endTime,
            attendeeCount,
            cancellationToken);
        return Ok(rooms);
    }

    [HttpGet("{roomId:guid}/schedule")]
    [ProducesResponseType<RoomScheduleDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RoomScheduleDto>> GetSchedule(
        Guid roomId,
        [FromQuery] DateTimeOffset startTime,
        [FromQuery] DateTimeOffset endTime,
        CancellationToken cancellationToken)
    {
        var schedule = await bookingService.GetRoomScheduleAsync(
            roomId,
            startTime,
            endTime,
            cancellationToken);
        return Ok(schedule);
    }
}
