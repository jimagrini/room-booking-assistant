using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomBooking.Domain.Rooms;

namespace RoomBooking.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(
        this IServiceProvider services,
        IReadOnlyCollection<RoomSeedDefinition> roomSeeds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(roomSeeds);
        if (roomSeeds.Count == 0)
        {
            throw new InvalidOperationException(
                "At least one room seed is required.");
        }

        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<RoomBookingDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);

        var existingIds = await dbContext.Rooms
            .Select(room => room.Id)
            .ToListAsync(cancellationToken);
        var existingIdSet = existingIds.ToHashSet();
        var missingRooms = roomSeeds
            .Where(seed => !existingIdSet.Contains(seed.Id))
            .Select(seed => new Room(
                seed.Id,
                seed.Name,
                seed.Capacity))
            .ToArray();

        if (missingRooms.Length == 0)
        {
            return;
        }

        await dbContext.Rooms.AddRangeAsync(
            missingRooms,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
