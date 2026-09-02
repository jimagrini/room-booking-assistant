using Microsoft.EntityFrameworkCore;
using Npgsql;
using RoomBooking.Application.Abstractions;
using RoomBooking.Application.Common;
using RoomBooking.Domain.Bookings;

namespace RoomBooking.Infrastructure.Persistence.Repositories;

internal sealed class BookingRepository(RoomBookingDbContext dbContext)
    : IBookingRepository
{
    private const string SlotConflictIndex =
        "ux_booking_slots_room_start";

    public async Task AddAsync(
        Booking booking,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Bookings.AddAsync(booking, cancellationToken);
    }

    public Task<Booking?> GetOwnedByIdAsync(
        Guid bookingId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Bookings
            .Include(booking => booking.Slots)
            .SingleOrDefaultAsync(
                booking => booking.Id == bookingId
                    && booking.UserId == userId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Booking>> ListOwnedByUserAsync(
        Guid userId,
        bool includeCancelled,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Bookings
            .AsNoTracking()
            .Where(booking => booking.UserId == userId);

        if (!includeCancelled)
        {
            query = query.Where(
                booking => booking.Status == BookingStatus.Active);
        }

        return await query
            .OrderBy(booking => booking.StartTimeUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Booking>> ListActiveForRoomAsync(
        Guid roomId,
        DateTimeOffset startTimeUtc,
        DateTimeOffset endTimeUtc,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Bookings
            .AsNoTracking()
            .Where(booking => booking.RoomId == roomId)
            .Where(booking => booking.Status == BookingStatus.Active)
            .Where(booking => booking.StartTimeUtc < endTimeUtc
                && booking.EndTimeUtc > startTimeUtc)
            .OrderBy(booking => booking.StartTimeUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsSlotConflict(exception))
        {
            throw new BookingConflictException(
                "The room is already booked for one or more requested slots.",
                exception);
        }
    }

    private static bool IsSlotConflict(Exception exception)
    {
        for (Exception? current = exception;
             current is not null;
             current = current.InnerException)
        {
            if (current is PostgresException postgresException
                && postgresException.SqlState
                    == PostgresErrorCodes.UniqueViolation
                && postgresException.ConstraintName == SlotConflictIndex)
            {
                return true;
            }

            if (current.Message.Contains(
                    SlotConflictIndex,
                    StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains(
                    "booking_slots.room_id, booking_slots.start_time_utc",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
