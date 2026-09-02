using RoomBooking.Domain.Bookings;

namespace RoomBooking.Application.Abstractions;

public interface IBookingRepository
{
    Task AddAsync(
        Booking booking,
        CancellationToken cancellationToken = default);

    Task<Booking?> GetOwnedByIdAsync(
        Guid bookingId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Booking>> ListOwnedByUserAsync(
        Guid userId,
        bool includeCancelled,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Booking>> ListActiveForRoomAsync(
        Guid roomId,
        DateTimeOffset startTimeUtc,
        DateTimeOffset endTimeUtc,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}
