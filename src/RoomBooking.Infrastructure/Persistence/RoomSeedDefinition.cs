namespace RoomBooking.Infrastructure.Persistence;

public sealed record RoomSeedDefinition(
    Guid Id,
    string Name,
    int Capacity);
