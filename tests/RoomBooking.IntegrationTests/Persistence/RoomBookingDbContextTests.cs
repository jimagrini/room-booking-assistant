using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RoomBooking.Domain.Bookings;
using RoomBooking.Domain.Rooms;
using RoomBooking.Infrastructure.Persistence;

namespace RoomBooking.IntegrationTests.Persistence;

public sealed class RoomBookingDbContextTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset StartTime =
        new(2026, 9, 3, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SaveChanges_PersistsBookingWithItsSlots()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);

        var room = new Room(Guid.NewGuid(), "Room A", 8);
        var booking = CreateBooking(room, StartTime, StartTime.AddHours(1));

        context.Rooms.Add(room);
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();

        var persisted = await context.Bookings
            .Include(item => item.Slots)
            .SingleAsync(item => item.Id == booking.Id);

        Assert.Equal(room.Id, persisted.RoomId);
        Assert.Equal(BookingStatus.Active, persisted.Status);
        Assert.Equal(2, persisted.Slots.Count);
    }

    [Fact]
    public async Task SaveChanges_WhenRoomSlotOverlaps_ThrowsDbUpdateException()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);

        var room = new Room(Guid.NewGuid(), "Room A", 8);
        var firstBooking =
            CreateBooking(room, StartTime, StartTime.AddHours(1));

        context.Rooms.Add(room);
        context.Bookings.Add(firstBooking);
        await context.SaveChangesAsync();

        var overlappingBooking =
            CreateBooking(room, StartTime.AddMinutes(30), StartTime.AddHours(1.5));

        context.Bookings.Add(overlappingBooking);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Cancel_ReleasesSlotsAndPreservesBookingHistory()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);

        var room = new Room(Guid.NewGuid(), "Room A", 8);
        var ownerId = Guid.NewGuid();
        var booking = CreateBooking(
            room,
            StartTime,
            StartTime.AddHours(1),
            ownerId);

        context.Rooms.Add(room);
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        booking.Cancel(ownerId, CreatedAt.AddHours(1));
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();

        var cancelledBooking = await context.Bookings
            .Include(item => item.Slots)
            .SingleAsync(item => item.Id == booking.Id);

        Assert.Equal(BookingStatus.Cancelled, cancelledBooking.Status);
        Assert.Empty(cancelledBooking.Slots);

        var replacement =
            CreateBooking(room, StartTime, StartTime.AddHours(1));

        context.Bookings.Add(replacement);
        await context.SaveChangesAsync();

        Assert.Equal(
            2,
            await context.BookingSlots.CountAsync(
                slot => slot.BookingId == replacement.Id));
    }

    private static Booking CreateBooking(
        Room room,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        Guid? ownerId = null)
    {
        return Booking.Create(
            Guid.NewGuid(),
            room,
            ownerId ?? Guid.NewGuid(),
            "Planning session",
            6,
            startTime,
            endTime,
            CreatedAt);
    }

    private static async Task<SqliteConnection> CreateOpenConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<RoomBookingDbContext> CreateContextAsync(
        SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<RoomBookingDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new RoomBookingDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }
}
