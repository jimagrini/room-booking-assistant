using RoomBooking.Application.Abstractions;
using RoomBooking.Application.Bookings;
using RoomBooking.Application.Common;
using RoomBooking.Domain.Bookings;
using RoomBooking.Domain.Rooms;

namespace RoomBooking.UnitTests.Application;

public sealed class BookingServiceTests
{
    private static readonly Guid UserId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid RoomId =
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3");
    private static readonly DateTimeOffset Now =
        new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateAsync_UsesAuthenticatedUserAndPersists()
    {
        var room = new Room(RoomId, "C", 8);
        var rooms = new FakeRoomRepository { Room = room };
        var bookings = new FakeBookingRepository();
        var service = CreateService(rooms, bookings);

        var result = await service.CreateAsync(
            new CreateBookingCommand(
                RoomId,
                "Architecture review",
                6,
                Now.AddHours(1),
                Now.AddHours(2)));

        Assert.Equal(UserId, result.UserId);
        Assert.Equal(RoomId, result.RoomId);
        Assert.Equal("Active", result.Status);
        Assert.Same(bookings.AddedBooking, bookings.AddedBooking);
        Assert.Equal(UserId, bookings.AddedBooking!.UserId);
        Assert.Equal(1, bookings.SaveCount);
    }

    [Fact]
    public async Task CreateAsync_WhenStartIsInPast_ReturnsStableError()
    {
        var rooms = new FakeRoomRepository
        {
            Room = new Room(RoomId, "C", 8)
        };
        var bookings = new FakeBookingRepository();
        var service = CreateService(rooms, bookings);

        var exception = await Assert.ThrowsAsync<
            RequestValidationException>(() =>
            service.CreateAsync(
                new CreateBookingCommand(
                    RoomId,
                    "Past meeting",
                    2,
                    Now.AddHours(-1),
                    Now)));

        Assert.Equal("booking.start_time_in_past", exception.Code);
        Assert.Null(bookings.AddedBooking);
        Assert.Equal(0, bookings.SaveCount);
    }

    [Fact]
    public async Task CancelAsync_UsesAuthenticatedUserAndReleasesSlots()
    {
        var room = new Room(RoomId, "C", 8);
        var booking = Booking.Create(
            Guid.NewGuid(),
            room,
            UserId,
            "Team sync",
            4,
            Now.AddHours(1),
            Now.AddHours(2),
            Now.AddDays(-1));
        var bookings = new FakeBookingRepository
        {
            OwnedBooking = booking
        };
        var service = CreateService(
            new FakeRoomRepository { Room = room },
            bookings);

        var result = await service.CancelAsync(booking.Id);

        Assert.Equal(UserId, bookings.LastRequestedUserId);
        Assert.Equal("Cancelled", result.Status);
        Assert.Empty(booking.Slots);
        Assert.Equal(1, bookings.SaveCount);
    }

    [Fact]
    public async Task GetRoomScheduleAsync_ReturnsOccupiedAndAvailableSlots()
    {
        var room = new Room(RoomId, "C", 8);
        var booking = Booking.Create(
            Guid.NewGuid(),
            room,
            UserId,
            "Occupied period",
            2,
            Now.AddMinutes(30),
            Now.AddMinutes(90),
            Now.AddDays(-1));
        var bookings = new FakeBookingRepository();
        bookings.ActiveRoomBookings.Add(booking);
        var service = CreateService(
            new FakeRoomRepository { Room = room },
            bookings);

        var result = await service.GetRoomScheduleAsync(
            RoomId,
            Now,
            Now.AddHours(2));

        Assert.Equal(4, result.Slots.Count);
        Assert.True(result.Slots[0].IsAvailable);
        Assert.False(result.Slots[1].IsAvailable);
        Assert.False(result.Slots[2].IsAvailable);
        Assert.True(result.Slots[3].IsAvailable);
    }

    private static BookingService CreateService(
        FakeRoomRepository rooms,
        FakeBookingRepository bookings)
    {
        return new BookingService(
            rooms,
            bookings,
            new FakeCurrentUser(UserId),
            new FixedTimeProvider(Now));
    }

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeRoomRepository : IRoomRepository
    {
        public Room? Room { get; init; }

        public IReadOnlyList<Room> AvailableRooms { get; init; } = [];

        public Task<Room?> GetByIdAsync(
            Guid roomId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                Room?.Id == roomId ? Room : null);
        }

        public Task<IReadOnlyList<Room>> ListAvailableAsync(
            DateTimeOffset startTimeUtc,
            DateTimeOffset endTimeUtc,
            int minimumCapacity,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(AvailableRooms);
        }
    }

    private sealed class FakeBookingRepository : IBookingRepository
    {
        public Booking? AddedBooking { get; private set; }

        public Booking? OwnedBooking { get; init; }

        public Guid? LastRequestedUserId { get; private set; }

        public List<Booking> ActiveRoomBookings { get; } = [];

        public int SaveCount { get; private set; }

        public Task AddAsync(
            Booking booking,
            CancellationToken cancellationToken = default)
        {
            AddedBooking = booking;
            return Task.CompletedTask;
        }

        public Task<Booking?> GetOwnedByIdAsync(
            Guid bookingId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            LastRequestedUserId = userId;
            var result = OwnedBooking?.Id == bookingId
                && OwnedBooking.UserId == userId
                    ? OwnedBooking
                    : null;
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<Booking>> ListOwnedByUserAsync(
            Guid userId,
            bool includeCancelled,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Booking> result = OwnedBooking is null
                ? []
                : [OwnedBooking];
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<Booking>> ListActiveForRoomAsync(
            Guid roomId,
            DateTimeOffset startTimeUtc,
            DateTimeOffset endTimeUtc,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Booking>>(
                ActiveRoomBookings);
        }

        public Task SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
