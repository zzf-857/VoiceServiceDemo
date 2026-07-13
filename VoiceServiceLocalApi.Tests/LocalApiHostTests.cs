using System.Net;
using System.Net.Sockets;
using VoiceServiceLocalApi;

namespace VoiceServiceLocalApi.Tests;

public sealed class LocalApiHostTests
{
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
}
