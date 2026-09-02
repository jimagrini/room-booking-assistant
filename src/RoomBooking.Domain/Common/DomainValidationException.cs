namespace RoomBooking.Domain.Common;

public sealed class DomainValidationException : Exception
{
    public DomainValidationException(string code, string message)
        : base(message)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("An error code is required.", nameof(code));
        }

        Code = code;
    }

    public string Code { get; }
}
