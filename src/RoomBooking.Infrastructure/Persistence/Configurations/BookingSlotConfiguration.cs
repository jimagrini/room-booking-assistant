using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomBooking.Domain.Bookings;
using RoomBooking.Domain.Rooms;

namespace RoomBooking.Infrastructure.Persistence.Configurations;

internal sealed class BookingSlotConfiguration
    : IEntityTypeConfiguration<BookingSlot>
{
    public void Configure(EntityTypeBuilder<BookingSlot> builder)
    {
        builder.ToTable("booking_slots");

        builder.HasKey(slot => slot.Id);

        builder.Property(slot => slot.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(slot => slot.BookingId)
            .HasColumnName("booking_id")
            .IsRequired();

        builder.Property(slot => slot.RoomId)
            .HasColumnName("room_id")
            .IsRequired();

        builder.Property(slot => slot.StartTimeUtc)
            .HasColumnName("start_time_utc")
            .IsRequired();

        builder.HasOne<Booking>()
            .WithMany(booking => booking.Slots)
            .HasForeignKey(slot => slot.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(slot => slot.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(slot => new
            {
                slot.RoomId,
                slot.StartTimeUtc
            })
            .IsUnique()
            .HasDatabaseName("ux_booking_slots_room_start");
    }
}
