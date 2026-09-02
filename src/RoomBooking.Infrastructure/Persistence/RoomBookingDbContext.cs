using Microsoft.EntityFrameworkCore;
using RoomBooking.Domain.Bookings;
using RoomBooking.Domain.Rooms;

namespace RoomBooking.Infrastructure.Persistence;

public sealed class RoomBookingDbContext(
    DbContextOptions<RoomBookingDbContext> options)
    : DbContext(options)
{
    public DbSet<Room> Rooms => Set<Room>();

    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<BookingSlot> BookingSlots => Set<BookingSlot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(RoomBookingDbContext).Assembly);
    }
}
