using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace RoomBooking.Api.Assistant;

public sealed class OpenAiResponsesClient(
    HttpClient httpClient,
    IOptions<AiProviderOptions> options,
    ILogger<OpenAiResponsesClient> logger)
    : IAiResponsesClient
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<OpenAiResponse> CreateResponseAsync(
        OpenAiResponseRequest request,
        CancellationToken cancellationToken = default)
    {
        var configuration = options.Value;
        ValidateConfiguration(configuration);

        var payload = new Dictionary<string, object?>
        {
            ["model"] = configuration.Model,
            ["instructions"] = request.Instructions,
            ["input"] = request.InputItems,
            ["tools"] = AssistantToolCatalog.Definitions,
            ["tool_choice"] = "auto",
            ["parallel_tool_calls"] = false,
            ["max_output_tokens"] = 1200
        };

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
                var providerError = ParseProviderError(
                    responseBody);
                logger.LogWarning(
                    "AI Responses API returned status {StatusCode}, code {ErrorCode}: {ErrorMessage}",
                    (int)httpResponse.StatusCode,
                    providerError.Code,
                    providerError.Message);
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
            var responseItems =
                new List<JsonElement>();
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
                    responseItems.Add(item.Clone());
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
                functionCalls,
                responseItems);
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

    private static ProviderError ParseProviderError(
        string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(
                responseBody);
            if (!document.RootElement.TryGetProperty(
                    "error",
                    out var error))
            {
                return new ProviderError(
                    "unknown",
                    "No provider error details were returned.");
            }

            var code = error.TryGetProperty(
                    "code",
                    out var codeElement)
                ? codeElement.GetString()
                : null;
            var message = error.TryGetProperty(
                    "message",
                    out var messageElement)
                ? messageElement.GetString()
                : null;

            return new ProviderError(
                code ?? "unknown",
                message ?? "No provider error message was returned.");
        }
        catch (JsonException)
        {
            return new ProviderError(
                "invalid_error_response",
                "The provider error response was not valid JSON.");
        }
    }

    private static void ValidateConfiguration(
        AiProviderOptions configuration)
    {
        if (string.IsNullOrWhiteSpace(
                configuration.ApiKey))
        {
            throw new AssistantException(
                StatusCodes.Status503ServiceUnavailable,
                "assistant.not_configured",
                "The AI provider API key is not configured.");
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
                "The AI provider configuration is invalid.");
        }
    }

    private sealed record ProviderError(
        string Code,
        string Message);
}
