using RoomBooking.Domain.Common;
using RoomBooking.Domain.Rooms;

namespace RoomBooking.UnitTests.Domain;

public sealed class RoomTests
{
    [Fact]
    public void Constructor_WithValidData_CreatesRoom()
    {
        var id = Guid.NewGuid();

        var room = new Room(id, "  Room A  ", 8);

        Assert.Equal(id, room.Id);
        Assert.Equal("Room A", room.Name);
        Assert.Equal(8, room.Capacity);
    }

    [Fact]
    public void Constructor_WithBlankName_ThrowsDomainValidationException()
    {
        var exception = Assert.Throws<DomainValidationException>(
            () => new Room(Guid.NewGuid(), "  ", 8));

        Assert.Equal("room.name_required", exception.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveCapacity_ThrowsDomainValidationException(
        int capacity)
    {
        var exception = Assert.Throws<DomainValidationException>(
            () => new Room(Guid.NewGuid(), "Room A", capacity));

        Assert.Equal("room.capacity_invalid", exception.Code);
    }
}
