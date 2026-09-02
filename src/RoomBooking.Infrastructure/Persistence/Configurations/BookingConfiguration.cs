using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomBooking.Domain.Bookings;
using RoomBooking.Domain.Rooms;

namespace RoomBooking.Infrastructure.Persistence.Configurations;

internal sealed class BookingConfiguration
    : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable(
            "bookings",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_bookings_attendee_count_positive",
                    "\"attendee_count\" > 0");
                table.HasCheckConstraint(
                    "ck_bookings_valid_time_range",
                    "\"end_time_utc\" > \"start_time_utc\"");
            });

        builder.HasKey(booking => booking.Id);

        builder.Property(booking => booking.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(booking => booking.RoomId)
            .HasColumnName("room_id")
            .IsRequired();

        builder.Property(booking => booking.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(booking => booking.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(booking => booking.AttendeeCount)
            .HasColumnName("attendee_count")
            .IsRequired();

        builder.Property(booking => booking.StartTimeUtc)
            .HasColumnName("start_time_utc")
            .IsRequired();

        builder.Property(booking => booking.EndTimeUtc)
            .HasColumnName("end_time_utc")
            .IsRequired();

        builder.Property(booking => booking.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(booking => booking.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(booking => booking.CancelledAtUtc)
            .HasColumnName("cancelled_at_utc");

        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(booking => booking.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(booking => new
            {
                booking.RoomId,
                booking.StartTimeUtc,
                booking.EndTimeUtc
            })
            .HasDatabaseName("ix_bookings_room_time_range");

        builder.HasIndex(booking => new
            {
                booking.UserId,
                booking.Status
            })
            .HasDatabaseName("ix_bookings_user_status");

        builder.Navigation(booking => booking.Slots)
            .HasField("_slots")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
