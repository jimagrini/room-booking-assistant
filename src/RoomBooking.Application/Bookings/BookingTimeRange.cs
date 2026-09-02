using RoomBooking.Application.Common;
using RoomBooking.Domain.Bookings;

namespace RoomBooking.Application.Bookings;

internal readonly record struct BookingTimeRange(
    DateTimeOffset StartTimeUtc,
    DateTimeOffset EndTimeUtc)
{
    private static readonly TimeSpan SlotDuration =
        TimeSpan.FromMinutes(Booking.SlotMinutes);

    private static readonly TimeSpan MaximumScheduleDuration =
        TimeSpan.FromDays(7);

    public static BookingTimeRange ForBooking(
        DateTimeOffset startTime,
        DateTimeOffset endTime)
    {
        return Create(
            startTime,
            endTime,
            TimeSpan.FromHours(Booking.MaximumDurationHours),
            "booking");
    }

    public static BookingTimeRange ForSchedule(
        DateTimeOffset startTime,
        DateTimeOffset endTime)
    {
        return Create(
            startTime,
            endTime,
            MaximumScheduleDuration,
            "schedule");
    }

    private static BookingTimeRange Create(
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        TimeSpan maximumDuration,
        string errorPrefix)
    {
        var startTimeUtc = startTime.ToUniversalTime();
        var endTimeUtc = endTime.ToUniversalTime();

        EnsureAligned(startTimeUtc, errorPrefix);
        EnsureAligned(endTimeUtc, errorPrefix);

        if (endTimeUtc <= startTimeUtc)
        {
            throw new RequestValidationException(
                $"{errorPrefix}.invalid_time_range",
                "End time must be after start time.");
        }

        var duration = endTimeUtc - startTimeUtc;

        if (duration < SlotDuration)
        {
            throw new RequestValidationException(
                $"{errorPrefix}.duration_too_short",
                $"The time range must be at least {Booking.SlotMinutes} minutes.");
        }

        if (duration > maximumDuration)
        {
            throw new RequestValidationException(
                $"{errorPrefix}.duration_exceeded",
                $"The requested time range cannot exceed {maximumDuration.TotalHours:0} hours.");
        }

        return new BookingTimeRange(startTimeUtc, endTimeUtc);
    }

    private static void EnsureAligned(
        DateTimeOffset valueUtc,
        string errorPrefix)
    {
        if (valueUtc.TimeOfDay.Ticks % SlotDuration.Ticks != 0)
        {
            throw new RequestValidationException(
                $"{errorPrefix}.time_unaligned",
                $"Times must align to {Booking.SlotMinutes}-minute boundaries.");
        }
    }
}
