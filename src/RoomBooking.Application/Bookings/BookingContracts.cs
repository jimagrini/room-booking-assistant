namespace RoomBooking.Application.Bookings;

public sealed record CreateBookingCommand(
    Guid RoomId,
    string Title,
    int AttendeeCount,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime);

public sealed record RoomDto(
    Guid Id,
    string Name,
    int Capacity);

public sealed record BookingDto(
    Guid Id,
    Guid RoomId,
    Guid UserId,
    string Title,
    int AttendeeCount,
    DateTimeOffset StartTimeUtc,
    DateTimeOffset EndTimeUtc,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CancelledAtUtc);

public sealed record RoomScheduleSlotDto(
    DateTimeOffset StartTimeUtc,
    DateTimeOffset EndTimeUtc,
    bool IsAvailable);

public sealed record RoomScheduleDto(
    RoomDto Room,
    DateTimeOffset StartTimeUtc,
    DateTimeOffset EndTimeUtc,
    IReadOnlyList<RoomScheduleSlotDto> Slots);
