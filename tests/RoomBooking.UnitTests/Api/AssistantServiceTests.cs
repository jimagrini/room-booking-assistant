using System.Text.Json;
using Microsoft.Extensions.Options;
using RoomBooking.Api.Assistant;
using RoomBooking.Application.Abstractions;
using RoomBooking.Application.Bookings;
using RoomBooking.Application.Common;
using RoomBooking.Infrastructure.Persistence;

namespace RoomBooking.UnitTests.Api;

public sealed class AssistantServiceTests
{
    private static readonly Guid User1Id =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid User2Id =
        Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Now =
        new(2026, 9, 2, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReplyAsync_WhenModelReturnsText_ReturnsNewConversation()
    {
        var client = new FakeResponsesClient(
            new OpenAiResponse(
                "response-1",
                "¿En qué horario necesitás la sala?",
                []));
        var service = CreateService(
            client,
            new FakeToolExecutor(),
            new AssistantConversationStore(
                new FixedTimeProvider(Now)),
            User1Id);

        var result = await service.ReplyAsync(
            "Necesito una sala");

        Assert.NotEqual(Guid.Empty, result.ConversationId);
        Assert.Equal(
            "¿En qué horario necesitás la sala?",
            result.Message);
        Assert.Empty(result.ToolsUsed);
        Assert.Single(client.Requests);
        Assert.Equal(
            "Necesito una sala",
            client.Requests[0].UserMessage);
    }

    [Fact]
    public async Task ReplyAsync_WhenModelCallsTool_ExecutesLoop()
    {
        var client = new FakeResponsesClient(
            new OpenAiResponse(
                "response-1",
                null,
                [
                    new OpenAiFunctionCall(
                        "call-1",
                        "list_my_bookings",
                        """{"include_cancelled":false}""")
                ]),
            new OpenAiResponse(
                "response-2",
                "No tenés reservas activas.",
                []));
        var toolExecutor = new FakeToolExecutor();
        var service = CreateService(
            client,
            toolExecutor,
            new AssistantConversationStore(
                new FixedTimeProvider(Now)),
            User1Id);

        var result = await service.ReplyAsync(
            "Mostrame mis reservas");

        Assert.Equal(
            "No tenés reservas activas.",
            result.Message);
        Assert.Equal(
            ["list_my_bookings"],
            result.ToolsUsed);
        Assert.Single(toolExecutor.Calls);
        Assert.Equal(2, client.Requests.Count);
        Assert.Equal(
            "response-1",
            client.Requests[1].PreviousResponseId);
        Assert.Null(client.Requests[1].UserMessage);
        Assert.Single(client.Requests[1].ToolOutputs);
        Assert.Equal(
            "call-1",
            client.Requests[1].ToolOutputs[0].CallId);
    }

    [Fact]
    public async Task ReplyAsync_ContinuesOwnedConversation()
    {
        var client = new FakeResponsesClient(
            new OpenAiResponse(
                "response-1",
                "¿Para cuántas personas?",
                []),
            new OpenAiResponse(
                "response-2",
                "Entendido.",
                []));
        var store = new AssistantConversationStore(
            new FixedTimeProvider(Now));
        var service = CreateService(
            client,
            new FakeToolExecutor(),
            store,
            User1Id);

        var first = await service.ReplyAsync(
            "Necesito una sala");
        await service.ReplyAsync(
            "Para cinco personas",
            first.ConversationId);

        Assert.Equal(2, client.Requests.Count);
        Assert.Equal(
            "response-1",
            client.Requests[1].PreviousResponseId);
    }

    [Fact]
    public async Task ReplyAsync_DoesNotShareConversationBetweenUsers()
    {
        var store = new AssistantConversationStore(
            new FixedTimeProvider(Now));
        var user1Service = CreateService(
            new FakeResponsesClient(
                new OpenAiResponse(
                    "response-1",
                    "Decime el horario.",
                    [])),
            new FakeToolExecutor(),
            store,
            User1Id);
        var first = await user1Service.ReplyAsync(
            "Necesito una sala");

        var user2Service = CreateService(
            new FakeResponsesClient(),
            new FakeToolExecutor(),
            store,
            User2Id);

        var exception = await Assert.ThrowsAsync<
            ResourceNotFoundException>(() =>
            user2Service.ReplyAsync(
                "Continuemos",
                first.ConversationId));

        Assert.Equal(
            "assistant.conversation_not_found",
            exception.Code);
    }

    [Fact]
    public async Task ReplyAsync_WithBlankMessage_ReturnsStableError()
    {
        var service = CreateService(
            new FakeResponsesClient(),
            new FakeToolExecutor(),
            new AssistantConversationStore(
                new FixedTimeProvider(Now)),
            User1Id);

        var exception = await Assert.ThrowsAsync<
            RequestValidationException>(() =>
            service.ReplyAsync("  "));

        Assert.Equal(
            "assistant.message_required",
            exception.Code);
    }

    [Fact]
    public void ToolCatalog_ExposesFiveStrictToolsWithoutUserId()
    {
        using var document = JsonDocument.Parse(
            AssistantToolCatalog.DefinitionsJson);
        var tools = document.RootElement
            .EnumerateArray()
            .ToArray();

        Assert.Equal(5, tools.Length);
        Assert.All(
            tools,
            tool =>
            {
                Assert.True(
                    tool.GetProperty("strict")
                        .GetBoolean());
                Assert.False(
                    tool.GetProperty("parameters")
                        .GetProperty("additionalProperties")
                        .GetBoolean());
            });
        Assert.False(
            AssistantToolCatalog.DefinitionsJson.Contains(
                "user_id",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateBookingTool_ResolvesRoomNameOnServer()
    {
        var bookingService =
            new RecordingBookingService();
        var roomId = Guid.Parse(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2");
        var executor = new AssistantToolExecutor(
            bookingService,
            [
                new RoomSeedDefinition(
                    roomId,
                    "B",
                    6)
            ]);

        var output = await executor.ExecuteAsync(
            new OpenAiFunctionCall(
                "call-1",
                "create_booking",
                """
                {
                  "room_name": "B",
                  "title": "Planning",
                  "attendee_count": 5,
                  "start_time": "2026-09-03T10:00:00-03:00",
                  "end_time": "2026-09-03T11:00:00-03:00"
                }
                """));

        Assert.Contains(
            "\"success\":true",
            output);
        Assert.NotNull(
            bookingService.LastCreateCommand);
        Assert.Equal(
            roomId,
            bookingService.LastCreateCommand.RoomId);
    }

    private static AssistantService CreateService(
        IOpenAiResponsesClient client,
        IAssistantToolExecutor toolExecutor,
        AssistantConversationStore store,
        Guid userId)
    {
        var timeProvider =
            new FixedTimeProvider(Now);
        return new AssistantService(
            client,
            toolExecutor,
            store,
            new FakeCurrentUser(userId),
            Options.Create(
                new OpenAiOptions
                {
                    OfficeTimeZone = "UTC"
                }),
            timeProvider);
    }

    private sealed class FakeResponsesClient(
        params OpenAiResponse[] responses)
        : IOpenAiResponsesClient
    {
        private readonly Queue<OpenAiResponse> _responses =
            new(responses);

        public List<OpenAiResponseRequest> Requests { get; } =
            [];

        public Task<OpenAiResponse> CreateResponseAsync(
            OpenAiResponseRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class FakeToolExecutor
        : IAssistantToolExecutor
    {
        public List<OpenAiFunctionCall> Calls { get; } = [];

        public Task<string> ExecuteAsync(
            OpenAiFunctionCall call,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(call);
            return Task.FromResult(
                """{"success":true,"data":[]}""");
        }
    }

    private sealed class FakeCurrentUser(Guid userId)
        : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    private sealed class FixedTimeProvider(
        DateTimeOffset now)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingBookingService
        : IBookingService
    {
        public CreateBookingCommand? LastCreateCommand
        {
            get;
            private set;
        }

        public Task<BookingDto> CreateAsync(
            CreateBookingCommand command,
            CancellationToken cancellationToken = default)
        {
            LastCreateCommand = command;
            return Task.FromResult(
                new BookingDto(
                    Guid.NewGuid(),
                    command.RoomId,
                    User1Id,
                    command.Title,
                    command.AttendeeCount,
                    command.StartTime,
                    command.EndTime,
                    "Active",
                    Now,
                    null));
        }

        public Task<IReadOnlyList<RoomDto>>
            ListAvailableRoomsAsync(
                DateTimeOffset startTime,
                DateTimeOffset endTime,
                int attendeeCount,
                CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<RoomScheduleDto>
            GetRoomScheduleAsync(
                Guid roomId,
                DateTimeOffset startTime,
                DateTimeOffset endTime,
                CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<BookingDto>>
            ListMyBookingsAsync(
                bool includeCancelled = false,
                CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<BookingDto> CancelAsync(
            Guid bookingId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
