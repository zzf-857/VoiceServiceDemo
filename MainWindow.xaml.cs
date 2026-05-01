using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.Versioning;
using VoiceServiceDemo.Services;

namespace VoiceServiceDemo;

[SupportedOSPlatform("windows7.0")]
public partial class MainWindow : Window
{
    public MainWindow()
    {
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
