namespace RoomBooking.Application.Bookings;

public interface IBookingService
{
    Task<BookingDto> CreateAsync(
        CreateBookingCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoomDto>> ListAvailableRoomsAsync(
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        int attendeeCount,
        CancellationToken cancellationToken = default);

    Task<RoomScheduleDto> GetRoomScheduleAsync(
        Guid roomId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BookingDto>> ListMyBookingsAsync(
        bool includeCancelled = false,
        CancellationToken cancellationToken = default);

    Task<BookingDto> CancelAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default);
}
