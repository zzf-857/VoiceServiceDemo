using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using VoiceServiceLocalApi;

namespace VoiceServiceLocalApi.Tests;

public sealed class LocalApiHttpTests
{
    private const string Token = "local-test-token";
    private static readonly JsonSerializerOptions SnakeCaseJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    [Fact]
    public async Task Health_is_anonymous_and_snake_case()
    {
        await using var api = await TestApiScope.StartAsync();
        using var response = await api.Client.GetAsync("/health");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"status\":\"healthy\"", json);
        Assert.Contains("\"timestamp\"", json);
    }

    [Fact]
    public async Task Openapi_declares_bearer_and_tts_routes_without_token_value()
    {
        await using var api = await TestApiScope.StartAsync();
        var json = await api.Client.GetStringAsync("/openapi/v1.json");

        Assert.Contains("bearer", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/tts", json);
        Assert.Contains("/api/v1/tts/audio", json);
        Assert.DoesNotContain(Token, json);
    }

    [Fact]
    public async Task Protected_route_rejects_missing_and_wrong_token()
    {
        await using var api = await TestApiScope.StartAsync();
        using var missing = await api.Client.GetAsync("/api/v1/vendors");
        using var wrongRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/vendors");
        wrongRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "wrong");
        using var wrong = await api.Client.SendAsync(wrongRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal("application/problem+json", missing.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Vendors_return_capabilities_without_credentials()
    {
        await using var api = await TestApiScope.StartAsync();
        using var response = await api.SendAuthorizedAsync(HttpMethod.Get, "/api/v1/vendors");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"configured\":true", json);
        Assert.Contains("supported_output_formats", json);
        Assert.DoesNotContain("api_key", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Token, json);
    }

    [Fact]
    public async Task Voice_refresh_is_forwarded_and_returns_snake_case_catalog()
    {
        await using var api = await TestApiScope.StartAsync();
        using var response = await api.SendAuthorizedAsync(HttpMethod.Get, "/api/v1/vendors/test/voices?refresh=true");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(api.Gateway.LastRefresh);
        Assert.Contains("voice-1", json);
        Assert.Contains("error_code", json);
    }

    [Fact]
    public async Task Voice_refresh_defaults_to_false_when_query_is_omitted()
    {
        await using var api = await TestApiScope.StartAsync();
        using var response = await api.SendAuthorizedAsync(HttpMethod.Get, "/api/v1/vendors/test/voices");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(api.Gateway.LastRefresh);
    }

    [Fact]
    public async Task Voice_refresh_preserves_unsupported_and_provider_error_codes()
    {
        await using var api = await TestApiScope.StartAsync();
        api.Gateway.VoiceErrorCode = "voice_fetch_not_supported";
        api.Gateway.VoiceErrorMessage = "unsupported";
        using var unsupported = await api.SendAuthorizedAsync(HttpMethod.Get, "/api/v1/vendors/test/voices?refresh=true");
        Assert.Equal(HttpStatusCode.BadRequest, unsupported.StatusCode);
        Assert.Contains("voice_fetch_not_supported", await unsupported.Content.ReadAsStringAsync());

        api.Gateway.VoiceErrorCode = "voice_fetch_failed";
        api.Gateway.VoiceErrorMessage = "upstream";
        using var failed = await api.SendAuthorizedAsync(HttpMethod.Get, "/api/v1/vendors/test/voices?refresh=true");
        Assert.Equal(HttpStatusCode.BadGateway, failed.StatusCode);
        Assert.Contains("voice_fetch_failed", await failed.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Json_generation_returns_metadata_and_a_protected_download()
    {
        await using var api = await TestApiScope.StartAsync();
        using var response = await api.PostTtsAsync("/api/v1/tts", ValidRequest());
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("audio/mpeg", json.RootElement.GetProperty("content_type").GetString());
        Assert.Equal(api.Gateway.GeneratedBytes.Length, json.RootElement.GetProperty("size_bytes").GetInt64());
        var audioUrl = new Uri(json.RootElement.GetProperty("audio_url").GetString()!);

        using var missingToken = await api.Client.GetAsync(audioUrl.PathAndQuery);
        using var downloaded = await api.SendAuthorizedAsync(HttpMethod.Get, audioUrl.PathAndQuery);
        Assert.Equal(HttpStatusCode.Unauthorized, missingToken.StatusCode);
        Assert.Equal(api.Gateway.GeneratedBytes, await downloaded.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Loopback_audio_url_ignores_an_untrusted_host_header()
    {
        await using var api = await TestApiScope.StartAsync();
        using var request = api.CreateTtsRequest("/api/v1/tts", ValidRequest());
        request.Headers.Host = "evil.example:5055";
        using var response = await api.Client.SendAsync(request);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.StartsWith("http://127.0.0.1:5055/", json.RootElement.GetProperty("audio_url").GetString());
    }

    [Fact]
    public async Task Binary_generation_returns_exact_bytes_mime_and_filename()
    {
        await using var api = await TestApiScope.StartAsync();
        using var response = await api.PostTtsAsync("/api/v1/tts/audio", ValidRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("audio/mpeg", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(api.Gateway.GeneratedBytes, await response.Content.ReadAsByteArrayAsync());
        Assert.NotNull(response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName);
    }

    [Fact]
    public async Task Audio_download_rejects_traversal()
    {
        await using var api = await TestApiScope.StartAsync();
        using var response = await api.SendAuthorizedAsync(HttpMethod.Get, "/api/v1/audio/..%5Csecret.mp3");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid_file_name", json);
    }

    [Fact]
    public async Task Audio_download_rejects_an_unregistered_output_file()
    {
        await using var api = await TestApiScope.StartAsync();
        await File.WriteAllBytesAsync(Path.Combine(api.Gateway.OutputDirectory, "orphan.mp3"), [9, 9, 9]);

        using var response = await api.SendAuthorizedAsync(HttpMethod.Get, "/api/v1/audio/orphan.mp3");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("file_not_found", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Request_body_larger_than_one_mebibyte_is_rejected()
    {
        await using var api = await TestApiScope.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tts");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        request.Content = new StringContent(new string('x', 1_048_577), Encoding.UTF8, "application/json");
        using var response = await api.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Contains("request_too_large", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Validation_and_gateway_errors_use_problem_json()
    {
        await using var api = await TestApiScope.StartAsync();
        using var validation = await api.PostTtsAsync("/api/v1/tts", new LocalTtsRequest("", "", ""));
        Assert.Equal(HttpStatusCode.BadRequest, validation.StatusCode);
        Assert.Equal("application/problem+json", validation.Content.Headers.ContentType?.MediaType);
        Assert.Contains("validation_error", await validation.Content.ReadAsStringAsync());

        api.Gateway.GenerateErrorCode = "credential_not_configured";
        api.Gateway.GenerateErrorMessage = "missing";
        using var gateway = await api.PostTtsAsync("/api/v1/tts", ValidRequest());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, gateway.StatusCode);
        Assert.Contains("credential_not_configured", await gateway.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Concurrent_generation_obeys_the_configured_limit()
    {
        var options = new LocalApiOptions
        {
            AccessToken = Token,
            Port = 5055,
            MaxConcurrentRequests = 1,
            MaxTextLength = 20_000
        };
        await using var api = await TestApiScope.StartAsync(options);
        api.Gateway.GenerateDelay = TimeSpan.FromMilliseconds(100);

        var first = api.PostTtsAsync("/api/v1/tts", ValidRequest());
        var second = api.PostTtsAsync("/api/v1/tts", ValidRequest());
        using var firstResponse = await first;
        using var secondResponse = await second;

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(1, api.Gateway.MaxObservedGenerations);
    }

    private static LocalTtsRequest ValidRequest() => new("test", "hello", "voice-1");

    private sealed class TestApiScope : IAsyncDisposable
    {
        private TestApiScope(WebApplication app, HttpClient client, FakeLocalTtsGateway gateway)
        {
            App = app;
            Client = client;
            Gateway = gateway;
        }

        public WebApplication App { get; }
        public HttpClient Client { get; }
        public FakeLocalTtsGateway Gateway { get; }

        public static LocalApiOptions CreateOptions() => new()
        {
            AccessToken = Token,
            Port = 5055,
            MaxConcurrentRequests = 2,
            MaxTextLength = 20_000
        };

        public static async Task<TestApiScope> StartAsync(LocalApiOptions? options = null)
        {
            var gateway = new FakeLocalTtsGateway();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.WebHost.UseTestServer();
            LocalApiApplication.ConfigureServices(builder.Services, options ?? CreateOptions(), gateway);
            var app = builder.Build();
            LocalApiApplication.Configure(app);
            await app.StartAsync();
            return new TestApiScope(app, app.GetTestClient(), gateway);
        }

        public HttpRequestMessage CreateTtsRequest(string path, LocalTtsRequest body)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
            request.Content = JsonContent.Create(body, options: SnakeCaseJson);
            return request;
        }

        public async Task<HttpResponseMessage> PostTtsAsync(string path, LocalTtsRequest body)
        {
            using var request = CreateTtsRequest(path, body);
            return await Client.SendAsync(request);
        }

        public async Task<HttpResponseMessage> SendAuthorizedAsync(HttpMethod method, string path)
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
            return await Client.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.StopAsync();
            await App.DisposeAsync();
            Gateway.Dispose();
        }
    }
}
