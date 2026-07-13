using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace VoiceServiceLocalApi;

public sealed class LocalApiHost : IAsyncDisposable
{
    private readonly LocalApiOptions _options;
    private readonly ILocalTtsGateway _gateway;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private WebApplication? _application;

    public LocalApiHost(LocalApiOptions options, ILocalTtsGateway gateway)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    }

    public bool IsRunning => _application is not null;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (_application is not null)
                return;

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ApplicationName = typeof(LocalApiHost).Assembly.FullName
            });
            builder.WebHost.ConfigureKestrel(kestrel =>
                kestrel.Limits.MaxRequestBodySize = LocalApiApplication.MaxRequestBodyBytes);
            var address = _options.AllowRemote ? "0.0.0.0" : "127.0.0.1";
            builder.WebHost.UseUrls($"http://{address}:{_options.Port}");
            LocalApiApplication.ConfigureServices(builder.Services, _options, _gateway);

            var app = builder.Build();
            LocalApiApplication.Configure(app);
            try
            {
                await app.StartAsync(cancellationToken);
                _application = app;
            }
            catch
            {
                await app.DisposeAsync();
                throw;
            }
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            var app = _application;
            if (app is null)
                return;

            _application = null;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                await app.StopAsync(timeout.Token);
            }
            finally
            {
                await app.DisposeAsync();
            }
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _lifecycleLock.Dispose();
    }
}
