using System.Text;
using VoiceServiceLocalApi;

namespace VoiceServiceLocalApi.Tests;

public sealed class TokenTests
{
    [Fact]
    public void Generated_token_has_256_bits_and_validates()
    {
        var token = LocalApiToken.Generate();
        var base64 = token.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');

        Assert.Equal(32, Convert.FromBase64String(base64).Length);
        Assert.True(LocalApiToken.Matches(token, token));
        Assert.False(LocalApiToken.Matches(token, token + "wrong"));
        Assert.False(LocalApiToken.Matches(token, ""));
    }

    [Fact]
    public void Options_validate_security_and_resource_limits()
    {
        var valid = new LocalApiOptions
        {
            AccessToken = LocalApiToken.Generate(),
            Port = 5055,
            MaxConcurrentRequests = 2,
            MaxTextLength = 20_000
        };

        Assert.Empty(valid.Validate());
        Assert.Contains(new LocalApiOptions { AccessToken = "", Port = 80 }.Validate(), error => error.Contains("token", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(new LocalApiOptions { AccessToken = "valid", Port = 70_000 }.Validate(), error => error.Contains("port", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(new LocalApiOptions { AccessToken = "valid", MaxConcurrentRequests = 0 }.Validate(), error => error.Contains("concurrent", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(new LocalApiOptions { AccessToken = "valid", MaxTextLength = 20_001 }.Validate(), error => error.Contains("text", StringComparison.OrdinalIgnoreCase));
    }
}
