using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RoomBooking.Infrastructure.Persistence;

public sealed class RoomBookingDbContextFactory
    : IDesignTimeDbContextFactory<RoomBookingDbContext>
{
    private const string ConnectionStringEnvironmentVariable =
        "ConnectionStrings__RoomBooking";

    public RoomBookingDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable)
            ?? "Host=localhost;Port=5432;Database=room_booking;Username=postgres";

        var options = new DbContextOptionsBuilder<RoomBookingDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new RoomBookingDbContext(options);
    }
}
