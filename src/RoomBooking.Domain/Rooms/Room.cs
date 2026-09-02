using RoomBooking.Domain.Common;

namespace RoomBooking.Domain.Rooms;

public sealed class Room
{
    private Room()
    {
    }

    public Room(Guid id, string name, int capacity)
    {
        if (id == Guid.Empty)
        {
            throw new DomainValidationException(
                "room.id_required",
                "A room identifier is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException(
                "room.name_required",
                "A room name is required.");
        }

        if (capacity <= 0)
        {
            throw new DomainValidationException(
                "room.capacity_invalid",
                "Room capacity must be greater than zero.");
        }

        Id = id;
        Name = name.Trim();
        Capacity = capacity;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public int Capacity { get; private set; }
}
