using RoomBooking.Domain.Common;
using RoomBooking.Domain.Rooms;

namespace RoomBooking.Domain.Bookings;

public sealed class Booking
{
    public const int SlotMinutes = 30;
    public const int MaximumDurationHours = 3;

    private static readonly TimeSpan SlotDuration = TimeSpan.FromMinutes(SlotMinutes);
    private static readonly TimeSpan MaximumDuration =
        TimeSpan.FromHours(MaximumDurationHours);

    private readonly List<BookingSlot> _slots = new();

    private Booking()
    {
    }

    private Booking(
        Guid id,
        Room room,
        Guid userId,
        string title,
        int attendeeCount,
        DateTimeOffset startTimeUtc,
        DateTimeOffset endTimeUtc,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        RoomId = room.Id;
        UserId = userId;
        Title = title;
        AttendeeCount = attendeeCount;
        StartTimeUtc = startTimeUtc;
        EndTimeUtc = endTimeUtc;
        CreatedAtUtc = createdAtUtc;
        Status = BookingStatus.Active;

        for (var slotStart = startTimeUtc;
             slotStart < endTimeUtc;
             slotStart = slotStart.Add(SlotDuration))
        {
            _slots.Add(new BookingSlot(
                Guid.NewGuid(),
                id,
                room.Id,
                slotStart));
        }
    }

    public Guid Id { get; private set; }

    public Guid RoomId { get; private set; }

    public Guid UserId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public int AttendeeCount { get; private set; }

    public DateTimeOffset StartTimeUtc { get; private set; }

    public DateTimeOffset EndTimeUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public BookingStatus Status { get; private set; }

    public DateTimeOffset? CancelledAtUtc { get; private set; }

    public IReadOnlyCollection<BookingSlot> Slots => _slots;

    public static Booking Create(
        Guid id,
        Room room,
        Guid userId,
        string title,
        int attendeeCount,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new DomainValidationException(
                "booking.id_required",
                "A booking identifier is required.");
        }

        if (room is null)
        {
            throw new DomainValidationException(
                "booking.room_required",
                "A room is required.");
        }

        if (userId == Guid.Empty)
        {
            throw new DomainValidationException(
                "booking.user_required",
                "A user identifier is required.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainValidationException(
                "booking.title_required",
                "A booking title is required.");
        }

        if (attendeeCount <= 0)
        {
            throw new DomainValidationException(
                "booking.attendee_count_invalid",
                "Attendee count must be greater than zero.");
        }

        if (attendeeCount > room.Capacity)
        {
            throw new DomainValidationException(
                "booking.capacity_exceeded",
                $"Room '{room.Name}' supports at most {room.Capacity} attendees.");
        }

        var startTimeUtc = startTime.ToUniversalTime();
        var endTimeUtc = endTime.ToUniversalTime();
        var createdAtUtc = createdAt.ToUniversalTime();

        EnsureAlignedToSlot(startTimeUtc, "booking.start_time_unaligned");
        EnsureAlignedToSlot(endTimeUtc, "booking.end_time_unaligned");

        if (endTimeUtc <= startTimeUtc)
        {
            throw new DomainValidationException(
                "booking.invalid_time_range",
                "Booking end time must be after its start time.");
        }

        var duration = endTimeUtc - startTimeUtc;

        if (duration < SlotDuration)
        {
            throw new DomainValidationException(
                "booking.duration_too_short",
                $"A booking must last at least {SlotMinutes} minutes.");
        }

        if (duration > MaximumDuration)
        {
            throw new DomainValidationException(
                "booking.duration_exceeded",
                $"A booking cannot last longer than {MaximumDurationHours} hours.");
        }

        return new Booking(
            id,
            room,
            userId,
            title.Trim(),
            attendeeCount,
            startTimeUtc,
            endTimeUtc,
            createdAtUtc);
    }

    public void Cancel(Guid requestingUserId, DateTimeOffset cancelledAt)
    {
        if (requestingUserId == Guid.Empty)
        {
            throw new DomainValidationException(
                "booking.requesting_user_required",
                "A requesting user identifier is required.");
        }

        if (requestingUserId != UserId)
        {
            throw new DomainValidationException(
                "booking.not_owner",
                "Only the user who created the booking can cancel it.");
        }

        if (Status == BookingStatus.Cancelled)
        {
            throw new DomainValidationException(
                "booking.already_cancelled",
                "The booking is already cancelled.");
        }

        var cancelledAtUtc = cancelledAt.ToUniversalTime();

        if (cancelledAtUtc < CreatedAtUtc)
        {
            throw new DomainValidationException(
                "booking.invalid_cancellation_time",
                "Cancellation time cannot be earlier than booking creation time.");
        }

        Status = BookingStatus.Cancelled;
        CancelledAtUtc = cancelledAtUtc;
    }

    private static void EnsureAlignedToSlot(
        DateTimeOffset valueUtc,
        string errorCode)
    {
        if (valueUtc.TimeOfDay.Ticks % SlotDuration.Ticks != 0)
        {
            throw new DomainValidationException(
                errorCode,
                $"Booking times must align to {SlotMinutes}-minute boundaries.");
        }
    }
}
