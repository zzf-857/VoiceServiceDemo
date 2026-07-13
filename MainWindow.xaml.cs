using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.Versioning;
using VoiceServiceDemo.Services;
using VoiceServiceLocalApi;
using System;

namespace VoiceServiceDemo;

[SupportedOSPlatform("windows7.0")]
public partial class MainWindow : Window
{
    private readonly ServiceProvider _serviceProvider;
    private readonly DesktopLocalApiService _localApiService;

    public MainWindow()
    {
        // 显式将 WebView2 的用户数据文件夹设置到 TEMP 目录，防止因 exe 目录写入或权限拦截导致闪退
        string tempUdfPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "VoiceOps_WebView2");
        System.Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", tempUdfPath);

        InitializeComponent();

        var serviceCollection = new ServiceCollection();

        // 注册服务
        serviceCollection.AddSingleton<SettingsService>();
        serviceCollection.AddSingleton<TtsService>();
        serviceCollection.AddSingleton<DesktopTtsGateway>();
        serviceCollection.AddSingleton<ILocalTtsGateway>(services => services.GetRequiredService<DesktopTtsGateway>());
        serviceCollection.AddSingleton<DesktopLocalApiService>();
        serviceCollection.AddWpfBlazorWebView();

#if DEBUG
        serviceCollection.AddBlazorWebViewDeveloperTools();
#endif

        _serviceProvider = serviceCollection.BuildServiceProvider();
        BlazorWebView.Services = _serviceProvider;
        _localApiService = _serviceProvider.GetRequiredService<DesktopLocalApiService>();
        Loaded += OnWindowLoaded;
    }

    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _localApiService.StartAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"本地 API 启动失败，桌面界面继续运行: {ex}");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        using var shutdownTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            Task.Run(() => _localApiService.StopAsync(shutdownTimeout.Token)).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"本地 API 停止失败或超时，继续释放桌面服务: {ex}");
        }
        finally
        {
            try
            {
                Task.Run(async () => await _serviceProvider.DisposeAsync())
                    .WaitAsync(shutdownTimeout.Token)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"桌面服务异步释放失败: {ex}");
            }
            finally
            {
                base.OnClosed(e);
            }
        }
    }
}
