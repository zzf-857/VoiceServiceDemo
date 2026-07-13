using System.Security.Cryptography;
using System.Text;

namespace VoiceServiceLocalApi;

public static class LocalApiToken
{
    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static bool Matches(string expected, string supplied)
    {
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(supplied))
            return false;

        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        return CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash);
    }
}
