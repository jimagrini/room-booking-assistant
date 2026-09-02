using Microsoft.EntityFrameworkCore;
using RoomBooking.Application.Abstractions;
using RoomBooking.Domain.Rooms;

namespace RoomBooking.Infrastructure.Persistence.Repositories;

internal sealed class RoomRepository(RoomBookingDbContext dbContext)
    : IRoomRepository
{
    public Task<Room?> GetByIdAsync(
        Guid roomId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Rooms
            .SingleOrDefaultAsync(
                room => room.Id == roomId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Room>> ListAvailableAsync(
        DateTimeOffset startTimeUtc,
        DateTimeOffset endTimeUtc,
        int minimumCapacity,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Rooms
            .AsNoTracking()
            .Where(room => room.Capacity >= minimumCapacity)
            .Where(room => !dbContext.BookingSlots.Any(
                slot => slot.RoomId == room.Id
                    && slot.StartTimeUtc >= startTimeUtc
                    && slot.StartTimeUtc < endTimeUtc))
            .OrderBy(room => room.Name)
            .ToListAsync(cancellationToken);
    }
}
