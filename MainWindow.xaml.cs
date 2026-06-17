using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.Versioning;
using VoiceServiceDemo.Services;
using System;

namespace VoiceServiceDemo;

[SupportedOSPlatform("windows7.0")]
public partial class MainWindow : Window
{
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
        serviceCollection.AddWpfBlazorWebView();

#if DEBUG
        serviceCollection.AddBlazorWebViewDeveloperTools();
#endif

        BlazorWebView.Services = serviceCollection.BuildServiceProvider();
    }
}
