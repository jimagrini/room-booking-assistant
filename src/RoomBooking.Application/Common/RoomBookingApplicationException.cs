namespace RoomBooking.Application.Common;

public abstract class RoomBookingApplicationException : Exception
{
    protected RoomBookingApplicationException(
        string code,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}

public sealed class RequestValidationException : RoomBookingApplicationException
{
    public RequestValidationException(string code, string message)
        : base(code, message)
    {
    }
}

public sealed class ResourceNotFoundException : RoomBookingApplicationException
{
    public ResourceNotFoundException(string code, string message)
        : base(code, message)
    {
    }
}

public sealed class BookingConflictException : RoomBookingApplicationException
{
    public BookingConflictException(
        string message,
        Exception? innerException = null)
        : base("booking.slot_conflict", message, innerException)
    {
    }
}
