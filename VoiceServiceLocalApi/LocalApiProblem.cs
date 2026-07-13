using Microsoft.AspNetCore.Http;

namespace VoiceServiceLocalApi;

public sealed record LocalApiProblem(
    string Type,
    string Title,
    int Status,
    string Code,
    string Detail,
    string RequestId,
    IReadOnlyList<LocalTtsValidationError>? Errors = null);

public static class LocalApiProblems
{
    public static IResult Result(
        HttpContext context,
        int status,
        string code,
        string detail,
        IReadOnlyList<LocalTtsValidationError>? errors = null)
    {
        var problem = new LocalApiProblem(
            $"https://voiceops.local/problems/{code}",
            GetTitle(code),
            status,
            code,
            detail,
            context.TraceIdentifier,
            errors);

        return Results.Json(
            problem,
            LocalApiApplication.JsonOptions,
            "application/problem+json",
            status);
    }

    public static int StatusFor(string? code) => code switch
    {
        "validation_error" or "voice_fetch_not_supported" or "invalid_file_name" => StatusCodes.Status400BadRequest,
        "unauthorized" => StatusCodes.Status401Unauthorized,
        "vendor_not_found" or "file_not_found" => StatusCodes.Status404NotFound,
        "concurrency_limit" => StatusCodes.Status409Conflict,
        "credential_not_configured" => StatusCodes.Status422UnprocessableEntity,
        "provider_timeout" => StatusCodes.Status504GatewayTimeout,
        "provider_error" or "voice_fetch_failed" => StatusCodes.Status502BadGateway,
        "request_too_large" => StatusCodes.Status413PayloadTooLarge,
        _ => StatusCodes.Status500InternalServerError
    };

    private static string GetTitle(string code) => code switch
    {
        "validation_error" => "Invalid TTS request",
        "unauthorized" => "Unauthorized",
        "vendor_not_found" => "Vendor not found",
        "file_not_found" => "Audio file not found",
        "invalid_file_name" => "Invalid audio file name",
        "credential_not_configured" => "Vendor credential is not configured",
        "provider_error" or "voice_fetch_failed" => "TTS provider request failed",
        "provider_timeout" => "TTS provider timed out",
        "request_too_large" => "Request body is too large",
        _ => "VoiceOps local API error"
    };
}
