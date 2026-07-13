using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

namespace VoiceServiceLocalApi;

public static class LocalApiApplication
{
    public const long MaxRequestBodyBytes = 1_048_576;

    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static void ConfigureServices(
        IServiceCollection services,
        LocalApiOptions options,
        ILocalTtsGateway gateway)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(gateway);

        var optionErrors = options.Validate();
        if (optionErrors.Count > 0)
            throw new ArgumentException(string.Join(" ", optionErrors), nameof(options));

        services.AddSingleton(options);
        services.AddSingleton(gateway);
        services.AddSingleton<ILocalTtsGateway>(gateway);
        services.AddSingleton<GeneratedAudioRegistry>();
        services.AddSingleton(_ => new SemaphoreSlim(options.MaxConcurrentRequests, options.MaxConcurrentRequests));
        services.ConfigureHttpJsonOptions(json =>
        {
            json.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
            json.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
        });
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(swagger =>
        {
            swagger.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "VoiceOps Local TTS API",
                Version = "v1",
                Description = "Generate speech through the TTS providers configured in the VoiceOps desktop app."
            });
            swagger.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "local API token",
                Description = "Authorization: Bearer <VoiceOps local API token>"
            });
        });
    }

    public static void Configure(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseSwagger(swagger => swagger.RouteTemplate = "openapi/{documentName}.json");

        app.Use(async (context, next) =>
        {
            context.TraceIdentifier = Guid.NewGuid().ToString("N");
            try
            {
                await next();
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                // The caller disconnected; do not attempt to replace an aborted response.
            }
            catch (OperationCanceledException)
            {
                if (!context.Response.HasStarted)
                    await LocalApiProblems.Result(context, StatusCodes.Status504GatewayTimeout, "provider_timeout", "The TTS provider did not complete before its timeout.").ExecuteAsync(context);
            }
            catch (Exception)
            {
                if (!context.Response.HasStarted)
                    await LocalApiProblems.Result(context, StatusCodes.Status500InternalServerError, "internal_error", "The local TTS API encountered an unexpected error.").ExecuteAsync(context);
            }
        });

        app.Use(async (context, next) =>
        {
            if (context.Request.ContentLength > MaxRequestBodyBytes)
            {
                await LocalApiProblems.Result(context, StatusCodes.Status413PayloadTooLarge, "request_too_large", "The request body cannot exceed 1 MiB.").ExecuteAsync(context);
                return;
            }

            await next();
        });

        app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments("/api/v1"))
            {
                await next();
                return;
            }

            var options = context.RequestServices.GetRequiredService<LocalApiOptions>();
            var header = context.Request.Headers.Authorization.ToString();
            if (!AuthenticationHeaderValue.TryParse(header, out var authorization) ||
                !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
                !LocalApiToken.Matches(options.AccessToken, authorization.Parameter ?? ""))
            {
                await LocalApiProblems.Result(context, StatusCodes.Status401Unauthorized, "unauthorized", "A valid Bearer token is required.").ExecuteAsync(context);
                return;
            }

            await next();
        });

        MapEndpoints(app);
    }

    private static void MapEndpoints(WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new
        {
            status = "healthy",
            version = "1.0.0",
            timestamp = DateTimeOffset.UtcNow
        }))
        .WithName("Health")
        .Produces(StatusCodes.Status200OK);

        app.MapGet("/api/v1/vendors", async (
            ILocalTtsGateway gateway,
            CancellationToken cancellationToken) =>
            Results.Ok(await gateway.GetVendorsAsync(cancellationToken)))
        .WithName("ListVendors")
        .Produces<IReadOnlyList<LocalVendorInfo>>(StatusCodes.Status200OK)
        .Produces<LocalApiProblem>(StatusCodes.Status401Unauthorized, "application/problem+json");

        app.MapGet("/api/v1/vendors/{vendor}/voices", async (
            string vendor,
            bool? refresh,
            HttpContext context,
            ILocalTtsGateway gateway,
            CancellationToken cancellationToken) =>
        {
            var catalog = await gateway.GetVoicesAsync(vendor, refresh ?? false, cancellationToken);
            if (catalog.Success)
                return Results.Ok(catalog);

            var status = LocalApiProblems.StatusFor(catalog.ErrorCode);
            return LocalApiProblems.Result(
                context,
                status,
                catalog.ErrorCode ?? "provider_error",
                catalog.ErrorMessage ?? "Unable to load the voice catalog.");
        })
        .WithName("ListVoices")
        .Produces<LocalVoiceCatalog>(StatusCodes.Status200OK)
        .Produces<LocalApiProblem>(StatusCodes.Status400BadRequest, "application/problem+json")
        .Produces<LocalApiProblem>(StatusCodes.Status502BadGateway, "application/problem+json");

        app.MapPost("/api/v1/tts", (
            LocalTtsRequest request,
            HttpContext context,
            ILocalTtsGateway gateway,
            LocalApiOptions options,
            SemaphoreSlim limiter) =>
            GenerateAsync(request, binary: false, context, gateway, options, limiter))
        .WithName("GenerateTts")
        .Accepts<LocalTtsRequest>("application/json")
        .Produces<LocalTtsResponse>(StatusCodes.Status200OK)
        .Produces<LocalApiProblem>(StatusCodes.Status400BadRequest, "application/problem+json")
        .Produces<LocalApiProblem>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")
        .Produces<LocalApiProblem>(StatusCodes.Status502BadGateway, "application/problem+json");

        app.MapPost("/api/v1/tts/audio", (
            LocalTtsRequest request,
            HttpContext context,
            ILocalTtsGateway gateway,
            LocalApiOptions options,
            SemaphoreSlim limiter) =>
            GenerateAsync(request, binary: true, context, gateway, options, limiter))
        .WithName("GenerateTtsAudio")
        .Accepts<LocalTtsRequest>("application/json")
        .Produces(StatusCodes.Status200OK, contentType: "audio/mpeg")
        .Produces<LocalApiProblem>(StatusCodes.Status400BadRequest, "application/problem+json")
        .Produces<LocalApiProblem>(StatusCodes.Status502BadGateway, "application/problem+json");

        app.MapGet("/api/v1/audio/{fileName}", (
            string fileName,
            HttpContext context,
            ILocalTtsGateway gateway,
            GeneratedAudioRegistry generatedAudio) =>
        {
            string filePath;
            try
            {
                if (!generatedAudio.TryResolve(gateway.OutputDirectory, fileName, out filePath))
                    return LocalApiProblems.Result(context, StatusCodes.Status404NotFound, "file_not_found", "The requested audio file does not exist.");
            }
            catch (ArgumentException ex)
            {
                return LocalApiProblems.Result(context, StatusCodes.Status400BadRequest, "invalid_file_name", ex.Message);
            }

            if (!File.Exists(filePath))
                return LocalApiProblems.Result(context, StatusCodes.Status404NotFound, "file_not_found", "The requested audio file does not exist.");

            return Results.File(
                filePath,
                AudioFileAccess.GetContentType(fileName),
                fileDownloadName: fileName,
                enableRangeProcessing: false);
        })
        .WithName("DownloadAudio")
        .Produces(StatusCodes.Status200OK)
        .Produces<LocalApiProblem>(StatusCodes.Status400BadRequest, "application/problem+json")
        .Produces<LocalApiProblem>(StatusCodes.Status404NotFound, "application/problem+json");
    }

    private static async Task<IResult> GenerateAsync(
        LocalTtsRequest request,
        bool binary,
        HttpContext context,
        ILocalTtsGateway gateway,
        LocalApiOptions options,
        SemaphoreSlim limiter)
    {
        var vendors = await gateway.GetVendorsAsync(context.RequestAborted);
        var vendor = vendors.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, request.Vendor, StringComparison.OrdinalIgnoreCase));
        var validation = LocalTtsRequestValidator.Validate(request, vendor, options.MaxTextLength);
        if (!validation.Success)
        {
            return LocalApiProblems.Result(
                context,
                StatusCodes.Status400BadRequest,
                "validation_error",
                "One or more TTS request fields are invalid.",
                validation.Errors);
        }

        await limiter.WaitAsync(context.RequestAborted);
        try
        {
            var result = await gateway.GenerateAsync(validation.NormalizedRequest!, context.RequestAborted);
            if (!result.Success)
            {
                var code = result.ErrorCode ?? "provider_error";
                return LocalApiProblems.Result(
                    context,
                    LocalApiProblems.StatusFor(code),
                    code,
                    result.ErrorMessage ?? "The TTS provider did not generate audio.");
            }

            if (string.IsNullOrWhiteSpace(result.FilePath))
                return LocalApiProblems.Result(context, StatusCodes.Status502BadGateway, "provider_error", "The TTS provider returned no audio file.");

            var fileName = Path.GetFileName(result.FilePath);
            var allowedPath = AudioFileAccess.Resolve(gateway.OutputDirectory, fileName);
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!string.Equals(Path.GetFullPath(result.FilePath), allowedPath, comparison) || !File.Exists(allowedPath))
                return LocalApiProblems.Result(context, StatusCodes.Status502BadGateway, "provider_error", "The generated audio file is unavailable.");

            var contentType = AudioFileAccess.GetContentType(fileName);
            if (binary)
            {
                var bytes = await File.ReadAllBytesAsync(allowedPath, context.RequestAborted);
                return Results.File(bytes, contentType, fileName, enableRangeProcessing: false);
            }

            var info = new FileInfo(allowedPath);
            var extension = info.Extension.TrimStart('.').ToLowerInvariant();
            var response = new LocalTtsResponse(
                context.TraceIdentifier,
                result.Vendor,
                result.ModelId,
                result.VoiceId,
                extension,
                contentType,
                info.Length,
                result.GeneratedAt,
                BuildAudioUrl(context, options, fileName));
            context.RequestServices.GetRequiredService<GeneratedAudioRegistry>()
                .Register(gateway.OutputDirectory, allowedPath);
            return Results.Ok(response);
        }
        finally
        {
            limiter.Release();
        }
    }

    private static string BuildAudioUrl(HttpContext context, LocalApiOptions options, string fileName)
    {
        var host = new HostString("127.0.0.1", options.Port);
        if (options.AllowRemote &&
            context.Request.Host.HasValue &&
            context.Request.Host.Port == options.Port &&
            Uri.CheckHostName(context.Request.Host.Host) != UriHostNameType.Unknown)
        {
            host = context.Request.Host;
        }

        return $"http://{host.ToUriComponent()}/api/v1/audio/{Uri.EscapeDataString(fileName)}";
    }
}
