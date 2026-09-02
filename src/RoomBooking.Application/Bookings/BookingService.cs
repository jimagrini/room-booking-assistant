using RoomBooking.Application.Abstractions;
using RoomBooking.Application.Common;
using RoomBooking.Domain.Bookings;

namespace RoomBooking.Application.Bookings;

public sealed class BookingService(
    IRoomRepository roomRepository,
    IBookingRepository bookingRepository,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : IBookingService
{
    public async Task<BookingDto> CreateAsync(
        CreateBookingCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var range = BookingTimeRange.ForBooking(
            command.StartTime,
            command.EndTime);
        var nowUtc = timeProvider.GetUtcNow();

        if (range.StartTimeUtc < nowUtc)
        {
            throw new RequestValidationException(
                "booking.start_time_in_past",
                "A booking cannot start in the past.");
        }

        var room = await roomRepository.GetByIdAsync(
            command.RoomId,
            cancellationToken);

        if (room is null)
        {
            throw new ResourceNotFoundException(
                "room.not_found",
                "The requested room was not found.");
        }

        var booking = Booking.Create(
            Guid.NewGuid(),
            room,
            currentUser.UserId,
            command.Title,
            command.AttendeeCount,
            range.StartTimeUtc,
            range.EndTimeUtc,
            nowUtc);

        await bookingRepository.AddAsync(booking, cancellationToken);
        await bookingRepository.SaveChangesAsync(cancellationToken);

        return MapBooking(booking);
    }

    public async Task<IReadOnlyList<RoomDto>> ListAvailableRoomsAsync(
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        int attendeeCount,
        CancellationToken cancellationToken = default)
    {
        if (attendeeCount <= 0)
        {
            throw new RequestValidationException(
                "booking.attendee_count_invalid",
                "Attendee count must be greater than zero.");
        }

        var range = BookingTimeRange.ForBooking(startTime, endTime);
        var rooms = await roomRepository.ListAvailableAsync(
            range.StartTimeUtc,
            range.EndTimeUtc,
            attendeeCount,
            cancellationToken);

        return rooms.Select(MapRoom).ToArray();
    }

    public async Task<RoomScheduleDto> GetRoomScheduleAsync(
        Guid roomId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default)
    {
        var range = BookingTimeRange.ForSchedule(startTime, endTime);
        var room = await roomRepository.GetByIdAsync(
            roomId,
            cancellationToken);

        if (room is null)
        {
            throw new ResourceNotFoundException(
                "room.not_found",
                "The requested room was not found.");
        }

        var bookings = await bookingRepository.ListActiveForRoomAsync(
            roomId,
            range.StartTimeUtc,
            range.EndTimeUtc,
            cancellationToken);

        var slots = new List<RoomScheduleSlotDto>();
        var slotDuration = TimeSpan.FromMinutes(Booking.SlotMinutes);

        for (var slotStart = range.StartTimeUtc;
             slotStart < range.EndTimeUtc;
             slotStart = slotStart.Add(slotDuration))
        {
            var slotEnd = slotStart.Add(slotDuration);
            var isOccupied = bookings.Any(
                booking => booking.StartTimeUtc < slotEnd
                    && booking.EndTimeUtc > slotStart);

            slots.Add(new RoomScheduleSlotDto(
                slotStart,
                slotEnd,
                !isOccupied));
        }

        return new RoomScheduleDto(
            MapRoom(room),
            range.StartTimeUtc,
            range.EndTimeUtc,
            slots);
    }

    public async Task<IReadOnlyList<BookingDto>> ListMyBookingsAsync(
        bool includeCancelled = false,
        CancellationToken cancellationToken = default)
    {
        var bookings = await bookingRepository.ListOwnedByUserAsync(
            currentUser.UserId,
            includeCancelled,
            cancellationToken);

        return bookings.Select(MapBooking).ToArray();
    }

    public async Task<BookingDto> CancelAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        if (bookingId == Guid.Empty)
        {
            throw new RequestValidationException(
                "booking.id_required",
                "A booking identifier is required.");
        }

        var booking = await bookingRepository.GetOwnedByIdAsync(
            bookingId,
            currentUser.UserId,
            cancellationToken);

        if (booking is null)
        {
            throw new ResourceNotFoundException(
                "booking.not_found",
                "The booking was not found.");
        }

        booking.Cancel(currentUser.UserId, timeProvider.GetUtcNow());
        await bookingRepository.SaveChangesAsync(cancellationToken);

        return MapBooking(booking);
    }

    private static RoomDto MapRoom(Domain.Rooms.Room room)
    {
        return new RoomDto(room.Id, room.Name, room.Capacity);
    }

    private static BookingDto MapBooking(Booking booking)
    {
        return new BookingDto(
            booking.Id,
            booking.RoomId,
            booking.UserId,
            booking.Title,
            booking.AttendeeCount,
            booking.StartTimeUtc,
            booking.EndTimeUtc,
            booking.Status.ToString(),
            booking.CreatedAtUtc,
            booking.CancelledAtUtc);
    }
}
