using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RoomBooking.Api.Assistant;
using RoomBooking.Application.Common;
using RoomBooking.Domain.Common;

namespace RoomBooking.Api.Errors;

internal sealed class ApiExceptionHandler(
    ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var error = exception switch
        {
            DomainValidationException domain =>
                new ApiError(400, domain.Code, domain.Message),
            RequestValidationException validation =>
                new ApiError(400, validation.Code, validation.Message),
            ResourceNotFoundException notFound =>
                new ApiError(404, notFound.Code, notFound.Message),
            BookingConflictException conflict =>
                new ApiError(409, conflict.Code, conflict.Message),
            AssistantException assistant =>
                new ApiError(
                    assistant.StatusCode,
                    assistant.Code,
                    assistant.Message),
            UnauthorizedAccessException unauthorized =>
                new ApiError(
                    401,
                    "auth.unauthorized",
                    unauthorized.Message),
            _ => null
        };

        if (error is null)
        {
            logger.LogError(
                exception,
                "Unhandled exception while processing a request.");
            return false;
        }

        logger.LogWarning(
            exception,
            "Request failed with {ErrorCode}.",
            error.Code);
        httpContext.Response.StatusCode = error.StatusCode;
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = error.StatusCode,
                Title = "The request could not be completed.",
                Detail = error.Message,
                Extensions = { ["code"] = error.Code }
            },
            cancellationToken);
        return true;
    }

    private sealed record ApiError(
        int StatusCode,
        string Code,
        string Message);
}
