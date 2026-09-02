using System.Text.Json;

namespace RoomBooking.Api.Assistant;

public static class AssistantToolCatalog
{
    public const string DefinitionsJson =
        """
        [
          {
            "type": "function",
            "name": "list_available_rooms",
            "description": "List rooms that are free for the complete requested interval and have enough capacity.",
            "strict": true,
            "parameters": {
              "type": "object",
              "properties": {
                "start_time": {
                  "type": "string",
                  "description": "ISO 8601 date-time with an explicit UTC offset."
                },
                "end_time": {
                  "type": "string",
                  "description": "ISO 8601 date-time with an explicit UTC offset."
                },
                "attendee_count": {
                  "type": "integer",
                  "description": "Positive number of attendees."
                }
              },
              "required": ["start_time", "end_time", "attendee_count"],
              "additionalProperties": false
            }
          },
          {
            "type": "function",
            "name": "get_room_schedule",
            "description": "Get the available and occupied 30-minute slots for one room over an interval.",
            "strict": true,
            "parameters": {
              "type": "object",
              "properties": {
                "room_name": {
                  "type": "string",
                  "enum": ["A", "B", "C", "D", "E"]
                },
                "start_time": {
                  "type": "string",
                  "description": "ISO 8601 date-time with an explicit UTC offset."
                },
                "end_time": {
                  "type": "string",
                  "description": "ISO 8601 date-time with an explicit UTC offset."
                }
              },
              "required": ["room_name", "start_time", "end_time"],
              "additionalProperties": false
            }
          },
          {
            "type": "function",
            "name": "list_my_bookings",
            "description": "List bookings owned by the authenticated user.",
            "strict": true,
            "parameters": {
              "type": "object",
              "properties": {
                "include_cancelled": {
                  "type": "boolean",
                  "description": "Whether cancelled bookings should be included."
                }
              },
              "required": ["include_cancelled"],
              "additionalProperties": false
            }
          },
          {
            "type": "function",
            "name": "create_booking",
            "description": "Create a meeting-room booking owned by the authenticated user.",
            "strict": true,
            "parameters": {
              "type": "object",
              "properties": {
                "room_name": {
                  "type": "string",
                  "enum": ["A", "B", "C", "D", "E"]
                },
                "title": {
                  "type": "string",
                  "description": "Non-empty meeting title."
                },
                "attendee_count": {
                  "type": "integer",
                  "description": "Positive number of attendees."
                },
                "start_time": {
                  "type": "string",
                  "description": "ISO 8601 date-time with an explicit UTC offset."
                },
                "end_time": {
                  "type": "string",
                  "description": "ISO 8601 date-time with an explicit UTC offset."
                }
              },
              "required": [
                "room_name",
                "title",
                "attendee_count",
                "start_time",
                "end_time"
              ],
              "additionalProperties": false
            }
          },
          {
            "type": "function",
            "name": "cancel_booking",
            "description": "Cancel an active booking owned by the authenticated user.",
            "strict": true,
            "parameters": {
              "type": "object",
              "properties": {
                "booking_id": {
                  "type": "string",
                  "description": "Booking UUID obtained from list_my_bookings."
                }
              },
              "required": ["booking_id"],
              "additionalProperties": false
            }
          }
        ]
        """;

    public static JsonElement Definitions { get; } =
        JsonDocument.Parse(DefinitionsJson)
            .RootElement
            .Clone();
}
