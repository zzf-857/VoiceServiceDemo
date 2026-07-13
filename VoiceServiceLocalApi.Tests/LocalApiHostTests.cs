using System.Net;
using System.Net.Sockets;
using VoiceServiceLocalApi;

namespace VoiceServiceLocalApi.Tests;

public sealed class LocalApiHostTests
{
    [Fact]
    public async Task Smoke_script_checks_a_real_host_without_echoing_the_token()
    {
        var scriptPath = Path.Combine(FindRepositoryRoot(), "scripts", "local_api_smoke.ps1");
        Assert.True(File.Exists(scriptPath), $"Smoke script is missing: {scriptPath}");

        var port = ReservePort();
        const string token = "smoke-secret-must-not-appear-in-output";
        using var gateway = new FakeLocalTtsGateway();
        await using var host = new LocalApiHost(new LocalApiOptions
        {
            AccessToken = token,
            Port = port,
            AllowRemote = false
        }, gateway);
        await host.StartAsync();

        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "pwsh",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add("-NoProfile");
        process.StartInfo.ArgumentList.Add("-File");
        process.StartInfo.ArgumentList.Add(scriptPath);
        process.StartInfo.ArgumentList.Add("-BaseUrl");
        process.StartInfo.ArgumentList.Add($"http://127.0.0.1:{port}");
        process.StartInfo.ArgumentList.Add("-Token");
        process.StartInfo.ArgumentList.Add(token);

        Assert.True(process.Start());
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(timeout.Token);
        var output = await standardOutput;
        var error = await standardError;

        Assert.True(process.ExitCode == 0, $"Smoke script failed with exit code {process.ExitCode}. stdout: {output} stderr: {error}");
        Assert.Contains("Local API smoke checks passed", output);
        Assert.DoesNotContain(token, output, StringComparison.Ordinal);
        Assert.DoesNotContain(token, error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Host_starts_stops_and_restarts_on_the_same_wrapper()
    {
        var port = ReservePort();
        using var gateway = new FakeLocalTtsGateway();
        await using var host = new LocalApiHost(new LocalApiOptions
        {
            AccessToken = "host-test-token",
            Port = port,
            AllowRemote = false
        }, gateway);

        await host.StartAsync();
        Assert.True(host.IsRunning);
        using (var client = new HttpClient())
        {
            var health = await client.GetStringAsync($"http://127.0.0.1:{port}/health");
            Assert.Contains("healthy", health);
        }

        await host.StopAsync();
        Assert.False(host.IsRunning);

        await host.StartAsync();
        Assert.True(host.IsRunning);
        using (var client = new HttpClient())
        {
            var health = await client.GetStringAsync($"http://127.0.0.1:{port}/health");
            Assert.Contains("healthy", health);
        }

        await host.StopAsync();
        Assert.False(host.IsRunning);
    }

    private static int ReservePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "VoiceServiceDemo.csproj")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
