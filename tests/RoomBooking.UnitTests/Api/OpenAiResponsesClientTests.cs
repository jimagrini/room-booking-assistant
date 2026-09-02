using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RoomBooking.Api.Assistant;

namespace RoomBooking.UnitTests.Api;

public sealed class OpenAiResponsesClientTests
{
    [Fact]
    public async Task CreateResponseAsync_UsesStatelessGroqCompatiblePayload()
    {
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        var client = new OpenAiResponsesClient(
            httpClient,
            Options.Create(
                new AiProviderOptions
                {
                    ApiKey = "gsk-test",
                    Model = "openai/gpt-oss-20b",
                    BaseUrl =
                        "https://api.groq.com/openai/v1/"
                }),
            NullLogger<OpenAiResponsesClient>.Instance);
        var userInput = JsonSerializer.SerializeToElement(
            new
            {
                role = "user",
                content = "Hola"
            });

        var response = await client.CreateResponseAsync(
            new OpenAiResponseRequest(
                "Reply briefly.",
                [userInput]));

        Assert.Equal("OK", response.OutputText);
        Assert.Equal(
            "https://api.groq.com/openai/v1/responses",
            handler.RequestUri?.ToString());

        using var payload = JsonDocument.Parse(
            Assert.IsType<string>(handler.RequestBody));
        var root = payload.RootElement;
        Assert.False(root.TryGetProperty("store", out _));
        Assert.False(
            root.TryGetProperty(
                "previous_response_id",
                out _));
        Assert.Equal(
            JsonValueKind.Array,
            root.GetProperty("input").ValueKind);
        Assert.Equal(
            "Hola",
            root.GetProperty("input")[0]
                .GetProperty("content")
                .GetString());
    }

    private sealed class RecordingHandler
        : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            RequestBody = await request.Content!
                .ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "id": "response-1",
                      "output": [
                        {
                          "type": "message",
                          "role": "assistant",
                          "content": [
                            {
                              "type": "output_text",
                              "text": "OK"
                            }
                          ]
                        }
                      ]
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
