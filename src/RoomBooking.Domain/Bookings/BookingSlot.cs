namespace RoomBooking.Domain.Bookings;

public sealed class BookingSlot
{
    private BookingSlot()
    {
    }

    internal BookingSlot(
        Guid id,
        Guid bookingId,
        Guid roomId,
        DateTimeOffset startTimeUtc)
    {
        Id = id;
        BookingId = bookingId;
        RoomId = roomId;
        StartTimeUtc = startTimeUtc;
    }

    public Guid Id { get; private set; }

    public Guid BookingId { get; private set; }

    public Guid RoomId { get; private set; }

    public DateTimeOffset StartTimeUtc { get; private set; }
}
