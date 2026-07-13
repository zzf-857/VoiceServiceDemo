using VoiceServiceLocalApi;

namespace VoiceServiceDemo.Services;

public enum LocalApiRuntimeStatus
{
    Stopped,
    Starting,
    Running,
    Stopping,
    Faulted
}

public sealed class DesktopLocalApiService : IAsyncDisposable
{
    private readonly SettingsService _settingsService;
    private readonly DesktopTtsGateway _gateway;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private LocalApiHost? _host;
    private LocalApiOptions? _activeOptions;

    public DesktopLocalApiService(SettingsService settingsService, DesktopTtsGateway gateway)
    {
        _settingsService = settingsService;
        _gateway = gateway;
    }

    public event EventHandler? StatusChanged;

    public LocalApiRuntimeStatus Status { get; private set; } = LocalApiRuntimeStatus.Stopped;
    public string? LastError { get; private set; }
    public bool IsRunning => Status == LocalApiRuntimeStatus.Running;
    public string? BaseUrl => _activeOptions is { } options
        ? $"http://127.0.0.1:{options.Port}"
        : null;
    public string? OpenApiUrl => BaseUrl is { } baseUrl
        ? $"{baseUrl}/openapi/v1.json"
        : null;

    public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await StartCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task<bool> RestartAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopCoreAsync(cancellationToken).ConfigureAwait(false);
            return await StartCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private async Task<bool> StartCoreAsync(CancellationToken cancellationToken)
    {
        if (_host is not null)
            return true;

        if (!_settingsService.Settings.LocalApi.Enabled)
        {
            _activeOptions = null;
            LastError = null;
            SetStatus(LocalApiRuntimeStatus.Stopped);
            return true;
        }

        LastError = null;
        SetStatus(LocalApiRuntimeStatus.Starting);
        var settings = _settingsService.Settings.LocalApi;
        var options = new LocalApiOptions
        {
            Enabled = settings.Enabled,
            Port = settings.Port,
            AllowRemote = settings.AllowRemote,
            AccessToken = settings.AccessToken,
            MaxConcurrentRequests = settings.MaxConcurrentRequests,
            MaxTextLength = settings.MaxTextLength
        };
        var host = new LocalApiHost(options, _gateway);
        try
        {
            await host.StartAsync(cancellationToken).ConfigureAwait(false);
            _host = host;
            _activeOptions = options;
            SetStatus(LocalApiRuntimeStatus.Running);
            return true;
        }
        catch (OperationCanceledException)
        {
            await host.DisposeAsync().ConfigureAwait(false);
            _activeOptions = null;
            SetStatus(LocalApiRuntimeStatus.Stopped);
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LastError = ex.Message;
            await host.DisposeAsync().ConfigureAwait(false);
            _activeOptions = null;
            SetStatus(LocalApiRuntimeStatus.Faulted);
            return false;
        }
    }

    private async Task StopCoreAsync(CancellationToken cancellationToken)
    {
        var host = _host;
        if (host is null)
        {
            _activeOptions = null;
            LastError = null;
            SetStatus(LocalApiRuntimeStatus.Stopped);
            return;
        }

        _host = null;
        _activeOptions = null;
        SetStatus(LocalApiRuntimeStatus.Stopping);
        try
        {
            await host.StopAsync(cancellationToken).ConfigureAwait(false);
            LastError = null;
            SetStatus(LocalApiRuntimeStatus.Stopped);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LastError = ex.Message;
            SetStatus(LocalApiRuntimeStatus.Faulted);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void SetStatus(LocalApiRuntimeStatus status)
    {
        if (Status == status)
            return;

        Status = status;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _lifecycleLock.Dispose();
    }
}
