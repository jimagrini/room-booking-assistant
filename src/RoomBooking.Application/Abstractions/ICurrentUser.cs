namespace RoomBooking.Application.Abstractions;

public interface ICurrentUser
{
    Guid UserId { get; }
}
