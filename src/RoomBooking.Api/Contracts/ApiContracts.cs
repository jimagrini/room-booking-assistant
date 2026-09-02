namespace RoomBooking.Api.Contracts;

public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    AuthenticatedUserDto User);

public sealed record AuthenticatedUserDto(
    Guid Id,
    string Username);

public sealed record CreateBookingRequest(
    Guid RoomId,
    string Title,
    int AttendeeCount,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime);
