using RoomBooking.Domain.Bookings;
using RoomBooking.Domain.Common;
using RoomBooking.Domain.Rooms;

namespace RoomBooking.UnitTests.Domain;

public sealed class BookingTests
{
    private static readonly Guid BookingId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset StartTime =
        new(2026, 9, 3, 14, 0, 0, TimeSpan.Zero);
    private static readonly Room Room = new(Guid.NewGuid(), "Room A", 8);

    [Fact]
    public void Create_WithValidData_CreatesActiveBookingAndSlots()
    {
        var booking = CreateBooking(
            startTime: StartTime,
            endTime: StartTime.AddHours(1));

        Assert.Equal(BookingId, booking.Id);
        Assert.Equal(Room.Id, booking.RoomId);
        Assert.Equal(OwnerId, booking.UserId);
        Assert.Equal("Planning session", booking.Title);
        Assert.Equal(6, booking.AttendeeCount);
        Assert.Equal(StartTime, booking.StartTimeUtc);
        Assert.Equal(StartTime.AddHours(1), booking.EndTimeUtc);
        Assert.Equal(CreatedAt, booking.CreatedAtUtc);
        Assert.Equal(BookingStatus.Active, booking.Status);
        Assert.Null(booking.CancelledAtUtc);

        var slots = booking.Slots.OrderBy(slot => slot.StartTimeUtc).ToArray();

        Assert.Equal(2, slots.Length);
        Assert.Equal(StartTime, slots[0].StartTimeUtc);
        Assert.Equal(StartTime.AddMinutes(30), slots[1].StartTimeUtc);
        Assert.All(slots, slot =>
        {
            Assert.Equal(BookingId, slot.BookingId);
            Assert.Equal(Room.Id, slot.RoomId);
        });
    }

    [Fact]
    public void Create_WithMaximumDuration_CreatesSixSlots()
    {
        var booking = CreateBooking(
            startTime: StartTime,
            endTime: StartTime.AddHours(Booking.MaximumDurationHours));

        Assert.Equal(6, booking.Slots.Count);
    }

    [Fact]
    public void Create_WithOffsetTimes_NormalizesValuesToUtc()
    {
        var localOffset = TimeSpan.FromHours(-3);
        var startWithOffset =
            new DateTimeOffset(2026, 9, 3, 11, 0, 0, localOffset);

        var booking = CreateBooking(
            startTime: startWithOffset,
            endTime: startWithOffset.AddMinutes(30));

        Assert.Equal(StartTime, booking.StartTimeUtc);
        Assert.Equal(TimeSpan.Zero, booking.StartTimeUtc.Offset);
        Assert.Equal(StartTime.AddMinutes(30), booking.EndTimeUtc);
    }

    [Fact]
    public void Create_WhenTitleIsBlank_ThrowsDomainValidationException()
    {
        var exception = Assert.Throws<DomainValidationException>(
            () => CreateBooking(title: "  "));

        Assert.Equal("booking.title_required", exception.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithNonPositiveAttendeeCount_ThrowsDomainValidationException(
        int attendeeCount)
    {
        var exception = Assert.Throws<DomainValidationException>(
            () => CreateBooking(attendeeCount: attendeeCount));

        Assert.Equal("booking.attendee_count_invalid", exception.Code);
    }

    [Fact]
    public void Create_WhenRoomCapacityIsExceeded_ThrowsDomainValidationException()
    {
        var exception = Assert.Throws<DomainValidationException>(
            () => CreateBooking(attendeeCount: Room.Capacity + 1));

        Assert.Equal("booking.capacity_exceeded", exception.Code);
    }

    [Fact]
    public void Create_WhenStartIsNotSlotAligned_ThrowsDomainValidationException()
    {
        var exception = Assert.Throws<DomainValidationException>(
            () => CreateBooking(
                startTime: StartTime.AddMinutes(15),
                endTime: StartTime.AddHours(1)));

        Assert.Equal("booking.start_time_unaligned", exception.Code);
    }

    [Fact]
    public void Create_WhenEndIsNotAfterStart_ThrowsDomainValidationException()
    {
        var exception = Assert.Throws<DomainValidationException>(
            () => CreateBooking(
                startTime: StartTime,
                endTime: StartTime));

        Assert.Equal("booking.invalid_time_range", exception.Code);
    }

    [Fact]
    public void Create_WhenDurationExceedsThreeHours_ThrowsDomainValidationException()
    {
        var exception = Assert.Throws<DomainValidationException>(
            () => CreateBooking(
                startTime: StartTime,
                endTime: StartTime.AddHours(3.5)));

        Assert.Equal("booking.duration_exceeded", exception.Code);
    }

    [Fact]
    public void Cancel_ByOwner_CancelsBooking()
    {
        var booking = CreateBooking();
        var cancelledAt = CreatedAt.AddHours(1);

        booking.Cancel(OwnerId, cancelledAt);

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(cancelledAt, booking.CancelledAtUtc);
    }

    [Fact]
    public void Cancel_ByDifferentUser_ThrowsDomainValidationException()
    {
        var booking = CreateBooking();

        var exception = Assert.Throws<DomainValidationException>(
            () => booking.Cancel(Guid.NewGuid(), CreatedAt.AddHours(1)));

        Assert.Equal("booking.not_owner", exception.Code);
        Assert.Equal(BookingStatus.Active, booking.Status);
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_ThrowsDomainValidationException()
    {
        var booking = CreateBooking();
        booking.Cancel(OwnerId, CreatedAt.AddHours(1));

        var exception = Assert.Throws<DomainValidationException>(
            () => booking.Cancel(OwnerId, CreatedAt.AddHours(2)));

        Assert.Equal("booking.already_cancelled", exception.Code);
    }

    private static Booking CreateBooking(
        string title = "Planning session",
        int attendeeCount = 6,
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null)
    {
        return Booking.Create(
            BookingId,
            Room,
            OwnerId,
            title,
            attendeeCount,
            startTime ?? StartTime,
            endTime ?? StartTime.AddHours(1),
            CreatedAt);
    }
}
