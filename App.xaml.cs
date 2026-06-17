using System;
using System.IO;
using System.Windows;

namespace VoiceServiceDemo;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            LogException(args.ExceptionObject as Exception);
        };

        base.OnStartup(e);
    }

    private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception);
        e.Handled = true;
    }

    private void LogException(Exception ex)
    {
        if (ex == null) return;
        try
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.txt");
            File.WriteAllText(logPath, $"{DateTime.Now}\n{ex.ToString()}\n\n");
            MessageBox.Show($"程序发生未处理的异常：\n{ex.Message}\n详细信息已写入 {logPath}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch
        {
            // Ignore log writing errors
        }
    }
}

