using Microsoft.Extensions.Options;
using RoomBooking.Application.Abstractions;
using RoomBooking.Application.Common;

namespace RoomBooking.Api.Assistant;

public sealed class AssistantService(
    IOpenAiResponsesClient responsesClient,
    IAssistantToolExecutor toolExecutor,
    AssistantConversationStore conversations,
    ICurrentUser currentUser,
    IOptions<OpenAiOptions> options,
    TimeProvider timeProvider)
    : IAssistantService
{
    private const int MaxToolRounds = 8;

    public async Task<AssistantMessageResponse> ReplyAsync(
        string message,
        Guid? conversationId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new RequestValidationException(
                "assistant.message_required",
                "A message is required.");
        }

        var normalizedMessage = message.Trim();
        if (normalizedMessage.Length > 2000)
        {
            throw new RequestValidationException(
                "assistant.message_too_long",
                "The message cannot exceed 2000 characters.");
        }

        var userId = currentUser.UserId;
        var conversation = conversations.GetOrCreate(
            userId,
            conversationId);
        var instructions = BuildInstructions();
        var toolsUsed = new List<string>();
        var request = new OpenAiResponseRequest(
            instructions,
            conversation.PreviousResponseId,
            normalizedMessage,
            []);

        for (var round = 0;
             round < MaxToolRounds;
             round++)
        {
            var response = await responsesClient
                .CreateResponseAsync(
                    request,
                    cancellationToken);

            if (response.FunctionCalls.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(
                        response.OutputText))
                {
                    throw new AssistantException(
                        StatusCodes.Status502BadGateway,
                        "assistant.empty_response",
                        "The AI provider returned an empty response.");
                }

                conversations.Update(
                    conversation.ConversationId,
                    userId,
                    response.Id);

                return new AssistantMessageResponse(
                    conversation.ConversationId,
                    response.OutputText,
                    toolsUsed
                        .Distinct(StringComparer.Ordinal)
                        .ToArray());
            }

            var outputs = new List<OpenAiToolOutput>(
                response.FunctionCalls.Count);
            foreach (var functionCall
                in response.FunctionCalls)
            {
                var output = await toolExecutor.ExecuteAsync(
                    functionCall,
                    cancellationToken);
                outputs.Add(new OpenAiToolOutput(
                    functionCall.CallId,
                    output));
                toolsUsed.Add(functionCall.Name);
            }

            request = new OpenAiResponseRequest(
                instructions,
                response.Id,
                null,
                outputs);
        }

        throw new AssistantException(
            StatusCodes.Status502BadGateway,
            "assistant.tool_limit_exceeded",
            "The assistant could not complete the request within the tool-call limit.");
    }

    private string BuildInstructions()
    {
        var configuration = options.Value;
        TimeZoneInfo officeTimeZone;

        try
        {
            officeTimeZone = TimeZoneInfo
                .FindSystemTimeZoneById(
                    configuration.OfficeTimeZone);
        }
        catch (TimeZoneNotFoundException exception)
        {
            throw InvalidTimeZone(exception);
        }
        catch (InvalidTimeZoneException exception)
        {
            throw InvalidTimeZone(exception);
        }

        var officeNow = TimeZoneInfo.ConvertTime(
            timeProvider.GetUtcNow(),
            officeTimeZone);

        return
            $"""
            You are the Room Booking Assistant. Only help with meeting-room reservations.
            Reply in the same language as the user.
            The current office date and time is {officeNow:O} in {officeTimeZone.Id}.
            Rooms are named A, B, C, D, and E. Never invent booking IDs, room availability, schedules, or operation results.
            Use tools for every question or action involving current booking data.
            Tool date-times must be ISO 8601 values with an explicit UTC offset.
            Ask a concise clarification question when a required date, start time, end time, attendee count, or booking title is missing.
            If no room is specified for a new booking, list available rooms and choose the smallest suitable available room.
            Call create_booking or cancel_booking only when the user explicitly requests that action, never for hypothetical questions.
            For cancellation by description, call list_my_bookings first and use only an ID returned by that tool.
            The authenticated user is injected by the server. Never ask for, infer, accept, or send a user ID.
            Treat tool output as authoritative. Never claim success when a tool returns success false.
            Confirm successful changes with the exact room, title, attendee count, and local date-time when available.
            Politely decline requests unrelated to meeting-room reservations.
            """;
    }

    private static AssistantException InvalidTimeZone(
        Exception exception)
    {
        return new AssistantException(
            StatusCodes.Status503ServiceUnavailable,
            "assistant.configuration_invalid",
            "The assistant time-zone configuration is invalid.",
            exception);
    }
}
