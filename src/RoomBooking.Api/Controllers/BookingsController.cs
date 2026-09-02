using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomBooking.Api.Contracts;
using RoomBooking.Application.Bookings;

namespace RoomBooking.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/bookings")]
public sealed class BookingsController(
    IBookingService bookingService)
    : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<BookingDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<BookingDto>> Create(
        CreateBookingRequest request,
        CancellationToken cancellationToken)
    {
        var booking = await bookingService.CreateAsync(
            new CreateBookingCommand(
                request.RoomId,
                request.Title,
                request.AttendeeCount,
                request.StartTime,
                request.EndTime),
            cancellationToken);
        return Created($"/api/bookings/{booking.Id}", booking);
    }

    [HttpGet("mine")]
    [ProducesResponseType<IReadOnlyList<BookingDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BookingDto>>> ListMine(
        [FromQuery] bool includeCancelled = false,
        CancellationToken cancellationToken = default)
    {
        var bookings = await bookingService.ListMyBookingsAsync(
            includeCancelled,
            cancellationToken);
        return Ok(bookings);
    }

    [HttpDelete("{bookingId:guid}")]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<BookingDto>> Cancel(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        var booking = await bookingService.CancelAsync(
            bookingId,
            cancellationToken);
        return Ok(booking);
    }
}
