using System.Collections.Concurrent;
using RoomBooking.Application.Common;

namespace RoomBooking.Api.Assistant;

public sealed record AssistantMessageRequest(
    string Message,
    Guid? ConversationId = null);

public sealed record AssistantMessageResponse(
    Guid ConversationId,
    string Message,
    IReadOnlyList<string> ToolsUsed);

public interface IAssistantService
{
    Task<AssistantMessageResponse> ReplyAsync(
        string message,
        Guid? conversationId = null,
        CancellationToken cancellationToken = default);
}

public interface IOpenAiResponsesClient
{
    Task<OpenAiResponse> CreateResponseAsync(
        OpenAiResponseRequest request,
        CancellationToken cancellationToken = default);
}

public interface IAssistantToolExecutor
{
    Task<string> ExecuteAsync(
        OpenAiFunctionCall call,
        CancellationToken cancellationToken = default);
}

public sealed record OpenAiResponseRequest(
    string Instructions,
    string? PreviousResponseId,
    string? UserMessage,
    IReadOnlyList<OpenAiToolOutput> ToolOutputs);

public sealed record OpenAiToolOutput(
    string CallId,
    string Output);

public sealed record OpenAiFunctionCall(
    string CallId,
    string Name,
    string Arguments);

public sealed record OpenAiResponse(
    string Id,
    string? OutputText,
    IReadOnlyList<OpenAiFunctionCall> FunctionCalls);

public sealed class OpenAiOptions
{
    public const string SectionName = "OpenAI";

    public string ApiKey { get; init; } = string.Empty;

    public string Model { get; init; } = "gpt-4.1-mini";

    public string BaseUrl { get; init; } =
        "https://api.openai.com/v1/";

    public string OfficeTimeZone { get; init; } =
        "America/Montevideo";
}

public sealed class AssistantException(
    int statusCode,
    string code,
    string message,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public int StatusCode { get; } = statusCode;

    public string Code { get; } = code;
}

public sealed record AssistantConversationState(
    Guid ConversationId,
    string? PreviousResponseId);

public sealed class AssistantConversationStore(
    TimeProvider timeProvider)
{
    private static readonly TimeSpan Lifetime =
        TimeSpan.FromHours(2);

    private readonly ConcurrentDictionary<Guid, ConversationEntry>
        _conversations = new();

    public AssistantConversationState GetOrCreate(
        Guid userId,
        Guid? conversationId)
    {
        var nowUtc = timeProvider.GetUtcNow();

        if (conversationId is null)
        {
            var id = Guid.NewGuid();
            _conversations[id] = new ConversationEntry(
                userId,
                null,
                nowUtc);
            return new AssistantConversationState(id, null);
        }

        if (!_conversations.TryGetValue(
                conversationId.Value,
                out var entry)
            || entry.UserId != userId
            || nowUtc - entry.UpdatedAtUtc > Lifetime)
        {
            if (entry is not null
                && nowUtc - entry.UpdatedAtUtc > Lifetime)
            {
                _conversations.TryRemove(
                    conversationId.Value,
                    out _);
            }

            throw new ResourceNotFoundException(
                "assistant.conversation_not_found",
                "The conversation was not found.");
        }

        return new AssistantConversationState(
            conversationId.Value,
            entry.PreviousResponseId);
    }

    public void Update(
        Guid conversationId,
        Guid userId,
        string responseId)
    {
        if (!_conversations.TryGetValue(
                conversationId,
                out var entry)
            || entry.UserId != userId)
        {
            throw new ResourceNotFoundException(
                "assistant.conversation_not_found",
                "The conversation was not found.");
        }

        _conversations[conversationId] = entry with
        {
            PreviousResponseId = responseId,
            UpdatedAtUtc = timeProvider.GetUtcNow()
        };
    }

    private sealed record ConversationEntry(
        Guid UserId,
        string? PreviousResponseId,
        DateTimeOffset UpdatedAtUtc);
}
