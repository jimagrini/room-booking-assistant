using System.Globalization;
using System.Text.Json;
using RoomBooking.Application.Bookings;
using RoomBooking.Application.Common;
using RoomBooking.Domain.Common;
using RoomBooking.Infrastructure.Persistence;

namespace RoomBooking.Api.Assistant;

public sealed class AssistantToolExecutor(
    IBookingService bookingService,
    IReadOnlyCollection<RoomSeedDefinition> roomSeeds)
    : IAssistantToolExecutor
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IReadOnlyDictionary<string, Guid> _roomIds =
        roomSeeds.ToDictionary(
            seed => seed.Name,
            seed => seed.Id,
            StringComparer.OrdinalIgnoreCase);

    public async Task<string> ExecuteAsync(
        OpenAiFunctionCall call,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var arguments = ParseArguments(call.Arguments);
            object result;

            switch (call.Name)
            {
                case "list_available_rooms":
                    result = await bookingService
                        .ListAvailableRoomsAsync(
                            RequiredDateTime(
                                arguments,
                                "start_time"),
                            RequiredDateTime(
                                arguments,
                                "end_time"),
                            RequiredInt32(
                                arguments,
                                "attendee_count"),
                            cancellationToken);
                    break;

                case "get_room_schedule":
                    result = await bookingService
                        .GetRoomScheduleAsync(
                            ResolveRoomId(arguments),
                            RequiredDateTime(
                                arguments,
                                "start_time"),
                            RequiredDateTime(
                                arguments,
                                "end_time"),
                            cancellationToken);
                    break;

                case "list_my_bookings":
                    result = await bookingService
                        .ListMyBookingsAsync(
                            RequiredBoolean(
                                arguments,
                                "include_cancelled"),
                            cancellationToken);
                    break;

                case "create_booking":
                    result = await bookingService.CreateAsync(
                        new CreateBookingCommand(
                            ResolveRoomId(arguments),
                            RequiredString(arguments, "title"),
                            RequiredInt32(
                                arguments,
                                "attendee_count"),
                            RequiredDateTime(
                                arguments,
                                "start_time"),
                            RequiredDateTime(
                                arguments,
                                "end_time")),
                        cancellationToken);
                    break;

                case "cancel_booking":
                    result = await bookingService.CancelAsync(
                        RequiredGuid(arguments, "booking_id"),
                        cancellationToken);
                    break;

                default:
                    throw new AssistantException(
                        StatusCodes.Status502BadGateway,
                        "assistant.unknown_tool",
                        "The AI provider requested an unknown tool.");
            }

            return JsonSerializer.Serialize(
                new { success = true, data = result },
                SerializerOptions);
        }
        catch (RoomBookingApplicationException exception)
        {
            return SerializeError(
                exception.Code,
                exception.Message);
        }
        catch (DomainValidationException exception)
        {
            return SerializeError(
                exception.Code,
                exception.Message);
        }
        catch (Exception exception)
            when (exception is JsonException
                or FormatException
                or OverflowException)
        {
            return SerializeError(
                "assistant.tool_arguments_invalid",
                "The tool arguments were invalid.");
        }
    }

    private Guid ResolveRoomId(JsonElement arguments)
    {
        var roomName = RequiredString(
                arguments,
                "room_name")
            .Trim();

        if (!_roomIds.TryGetValue(roomName, out var roomId))
        {
            throw new ResourceNotFoundException(
                "room.not_found",
                "The requested room was not found.");
        }

        return roomId;
    }

    private static JsonElement ParseArguments(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind
            != JsonValueKind.Object)
        {
            throw new JsonException(
                "Tool arguments must be an object.");
        }

        return document.RootElement.Clone();
    }

    private static string RequiredString(
        JsonElement arguments,
        string propertyName)
    {
        if (!arguments.TryGetProperty(
                propertyName,
                out var property)
            || property.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(
                property.GetString()))
        {
            throw new JsonException(
                $"'{propertyName}' must be a non-empty string.");
        }

        return property.GetString()!;
    }

    private static int RequiredInt32(
        JsonElement arguments,
        string propertyName)
    {
        if (!arguments.TryGetProperty(
                propertyName,
                out var property)
            || !property.TryGetInt32(out var value))
        {
            throw new JsonException(
                $"'{propertyName}' must be an integer.");
        }

        return value;
    }

    private static bool RequiredBoolean(
        JsonElement arguments,
        string propertyName)
    {
        if (!arguments.TryGetProperty(
                propertyName,
                out var property)
            || property.ValueKind is not (
                JsonValueKind.True
                or JsonValueKind.False))
        {
            throw new JsonException(
                $"'{propertyName}' must be a boolean.");
        }

        return property.GetBoolean();
    }

    private static Guid RequiredGuid(
        JsonElement arguments,
        string propertyName)
    {
        var value = RequiredString(arguments, propertyName);
        if (!Guid.TryParse(value, out var id)
            || id == Guid.Empty)
        {
            throw new JsonException(
                $"'{propertyName}' must be a UUID.");
        }

        return id;
    }

    private static DateTimeOffset RequiredDateTime(
        JsonElement arguments,
        string propertyName)
    {
        var value = RequiredString(arguments, propertyName);
        var hasExplicitOffset =
            value.EndsWith(
                "Z",
                StringComparison.OrdinalIgnoreCase)
            || value.LastIndexOf('+') > 9
            || value.LastIndexOf('-') > 9;

        if (!hasExplicitOffset
            || !DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var dateTime))
        {
            throw new JsonException(
                $"'{propertyName}' must be an ISO 8601 date-time with an explicit offset.");
        }

        return dateTime;
    }

    private static string SerializeError(
        string code,
        string message)
    {
        return JsonSerializer.Serialize(
            new
            {
                success = false,
                error = new { code, message }
            },
            SerializerOptions);
    }
}
