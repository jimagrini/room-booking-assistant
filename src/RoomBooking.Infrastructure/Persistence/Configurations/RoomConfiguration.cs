using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomBooking.Domain.Rooms;

namespace RoomBooking.Infrastructure.Persistence.Configurations;

internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable(
            "rooms",
            table => table.HasCheckConstraint(
                "ck_rooms_capacity_positive",
                "\"capacity\" > 0"));

        builder.HasKey(room => room.Id);

        builder.Property(room => room.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(room => room.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(room => room.Capacity)
            .HasColumnName("capacity")
            .IsRequired();

        builder.HasIndex(room => room.Name)
            .IsUnique()
            .HasDatabaseName("ux_rooms_name");
    }
}
