using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace RoomBooking.Api.Assistant;

public sealed class OpenAiResponsesClient(
    HttpClient httpClient,
    IOptions<OpenAiOptions> options,
    ILogger<OpenAiResponsesClient> logger)
    : IOpenAiResponsesClient
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<OpenAiResponse> CreateResponseAsync(
        OpenAiResponseRequest request,
        CancellationToken cancellationToken = default)
    {
        var configuration = options.Value;
        ValidateConfiguration(configuration);

        object input = request.UserMessage is not null
            ? request.UserMessage
            : request.ToolOutputs
                .Select(output => new
                {
                    type = "function_call_output",
                    call_id = output.CallId,
                    output = output.Output
                })
                .ToArray();

        var payload = new Dictionary<string, object?>
        {
            ["model"] = configuration.Model,
            ["instructions"] = request.Instructions,
            ["input"] = input,
            ["tools"] = AssistantToolCatalog.Definitions,
            ["tool_choice"] = "auto",
            ["parallel_tool_calls"] = false,
            ["store"] = true,
            ["max_output_tokens"] = 1200
        };

        if (!string.IsNullOrWhiteSpace(
                request.PreviousResponseId))
        {
            payload["previous_response_id"] =
                request.PreviousResponseId;
        }

        var baseUrl = configuration.BaseUrl.EndsWith('/')
            ? configuration.BaseUrl
            : configuration.BaseUrl + "/";
        var endpoint = new Uri(
            new Uri(baseUrl, UriKind.Absolute),
            "responses");

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            endpoint);
        httpRequest.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                configuration.ApiKey);
        httpRequest.Content = JsonContent.Create(
            payload,
            options: SerializerOptions);

        HttpResponseMessage httpResponse;
        try
        {
            httpResponse = await httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }
        catch (TaskCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new AssistantException(
                StatusCodes.Status504GatewayTimeout,
                "assistant.provider_timeout",
                "The AI provider did not respond in time.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new AssistantException(
                StatusCodes.Status502BadGateway,
                "assistant.provider_unavailable",
                "The AI provider is unavailable.",
                exception);
        }

        using (httpResponse)
        {
            var responseBody = await httpResponse.Content
                .ReadAsStringAsync(cancellationToken);

            if (!httpResponse.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "OpenAI Responses API returned status {StatusCode}.",
                    (int)httpResponse.StatusCode);
                throw new AssistantException(
                    StatusCodes.Status502BadGateway,
                    "assistant.provider_error",
                    "The AI provider could not complete the request.");
            }

            return ParseResponse(responseBody);
        }
    }

    private static OpenAiResponse ParseResponse(
        string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(
                responseBody);
            var root = document.RootElement;
            var responseId = root.GetProperty("id")
                .GetString();

            if (string.IsNullOrWhiteSpace(responseId))
            {
                throw new JsonException(
                    "The response identifier is missing.");
            }

            var functionCalls =
                new List<OpenAiFunctionCall>();
            var text = new StringBuilder();

            if (root.TryGetProperty(
                    "output",
                    out var outputItems)
                && outputItems.ValueKind
                    == JsonValueKind.Array)
            {
                foreach (var item
                    in outputItems.EnumerateArray())
                {
                    var type = item.GetProperty("type")
                        .GetString();

                    if (type == "function_call")
                    {
                        functionCalls.Add(
                            new OpenAiFunctionCall(
                                item.GetProperty("call_id")
                                    .GetString()
                                    ?? throw new JsonException(
                                        "Tool call ID is missing."),
                                item.GetProperty("name")
                                    .GetString()
                                    ?? throw new JsonException(
                                        "Tool name is missing."),
                                item.GetProperty("arguments")
                                    .GetString()
                                    ?? "{}"));
                        continue;
                    }

                    if (type != "message"
                        || !item.TryGetProperty(
                            "content",
                            out var contentItems)
                        || contentItems.ValueKind
                            != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var content
                        in contentItems.EnumerateArray())
                    {
                        if (content.GetProperty("type")
                                .GetString()
                            != "output_text")
                        {
                            continue;
                        }

                        var value = content
                            .GetProperty("text")
                            .GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            if (text.Length > 0)
                            {
                                text.AppendLine();
                            }

                            text.Append(value);
                        }
                    }
                }
            }

            return new OpenAiResponse(
                responseId,
                text.Length == 0
                    ? null
                    : text.ToString(),
                functionCalls);
        }
        catch (JsonException exception)
        {
            throw new AssistantException(
                StatusCodes.Status502BadGateway,
                "assistant.provider_response_invalid",
                "The AI provider returned an invalid response.",
                exception);
        }
    }

    private static void ValidateConfiguration(
        OpenAiOptions configuration)
    {
        if (string.IsNullOrWhiteSpace(
                configuration.ApiKey))
        {
            throw new AssistantException(
                StatusCodes.Status503ServiceUnavailable,
                "assistant.not_configured",
                "The OpenAI API key is not configured.");
        }

        if (string.IsNullOrWhiteSpace(
                configuration.Model)
            || !Uri.TryCreate(
                configuration.BaseUrl,
                UriKind.Absolute,
                out _))
        {
            throw new AssistantException(
                StatusCodes.Status503ServiceUnavailable,
                "assistant.configuration_invalid",
                "The OpenAI configuration is invalid.");
        }
    }
}
