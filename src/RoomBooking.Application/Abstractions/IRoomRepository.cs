using RoomBooking.Domain.Rooms;

namespace RoomBooking.Application.Abstractions;

public interface IRoomRepository
{
    Task<Room?> GetByIdAsync(
        Guid roomId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Room>> ListAvailableAsync(
        DateTimeOffset startTimeUtc,
        DateTimeOffset endTimeUtc,
        int minimumCapacity,
        CancellationToken cancellationToken = default);
}
