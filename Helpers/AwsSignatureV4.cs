using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace VoiceServiceDemo.Helpers;

public sealed record AwsPollyCredentials
{
    private static readonly Regex RegionPattern = new(
        "^[A-Za-z0-9-]+$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private AwsPollyCredentials(
        string accessKeyId,
        string secretAccessKey,
        string region,
        string? sessionToken)
    {
        AccessKeyId = accessKeyId;
        SecretAccessKey = secretAccessKey;
        Region = region;
        SessionToken = sessionToken;
    }

    public string AccessKeyId { get; }
    public string SecretAccessKey { get; }
    public string Region { get; }
    public string? SessionToken { get; }
    public bool HasSessionToken => !string.IsNullOrWhiteSpace(SessionToken);

    public static AwsPollyCredentials Parse(string? value)
    {
        var parts = (value ?? string.Empty).Split('|');
        if (parts.Length is not (3 or 4))
            throw InvalidFormat();

        var accessKeyId = parts[0].Trim();
        var secretAccessKey = parts[1].Trim();
        var region = parts[2].Trim();
        var sessionToken = parts.Length == 4 ? parts[3].Trim() : string.Empty;

        if (string.IsNullOrWhiteSpace(accessKeyId) ||
            string.IsNullOrWhiteSpace(secretAccessKey) ||
            string.IsNullOrWhiteSpace(region) ||
            !RegionPattern.IsMatch(region) ||
            ContainsControlCharacters(accessKeyId) ||
            ContainsControlCharacters(secretAccessKey) ||
            ContainsControlCharacters(sessionToken))
        {
            throw InvalidFormat();
        }

        return new AwsPollyCredentials(
            accessKeyId,
            secretAccessKey,
            region,
            string.IsNullOrWhiteSpace(sessionToken) ? null : sessionToken);
    }

    public override string ToString() =>
        HasSessionToken
            ? $"{AccessKeyId}|***|{Region}|***"
            : $"{AccessKeyId}|***|{Region}";

    private static ArgumentException InvalidFormat() => new(
        "Amazon Polly 凭证格式应为 access_key_id|secret_access_key|region[|session_token]，region 仅允许字母、数字和连字符。",
        "value");

    private static bool ContainsControlCharacters(string value) => value.Any(char.IsControl);
}

public sealed record AwsSignatureResult(
    string Authorization,
    string AmzDate,
    string PayloadHash,
    string SignedHeaders,
    string CanonicalRequest)
{
    public override string ToString() =>
        $"AwsSignatureResult {{ AmzDate = {AmzDate}, PayloadHash = {PayloadHash}, SignedHeaders = {SignedHeaders}, Authorization = ***, CanonicalRequest = *** }}";
}

public static class AwsSignatureV4
{
    private const string Algorithm = "AWS4-HMAC-SHA256";

    public static AwsSignatureResult Sign(
        HttpMethod method,
        Uri uri,
        ReadOnlySpan<byte> payload,
        AwsPollyCredentials credentials,
        string service,
        DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentException.ThrowIfNullOrWhiteSpace(service);
        if (!uri.IsAbsoluteUri || string.IsNullOrWhiteSpace(uri.Host))
            throw new ArgumentException("AWS SigV4 requires an absolute URI with a host.", nameof(uri));

        var utcTimestamp = timestamp.ToUniversalTime();
        var amzDate = utcTimestamp.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var dateStamp = utcTimestamp.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var payloadHash = Sha256Hex(payload);
        var canonicalUri = CanonicalizePath(uri);
        var canonicalQuery = CanonicalizeQuery(uri);
        var host = uri.IsDefaultPort ? uri.IdnHost : $"{uri.IdnHost}:{uri.Port}";

        var canonicalHeaders = new StringBuilder()
            .Append("host:").Append(host.ToLowerInvariant()).Append('\n')
            .Append("x-amz-date:").Append(amzDate).Append('\n');
        var signedHeaders = "host;x-amz-date";
        if (credentials.HasSessionToken)
        {
            canonicalHeaders.Append("x-amz-security-token:").Append(credentials.SessionToken).Append('\n');
            signedHeaders += ";x-amz-security-token";
        }

        var canonicalRequest = string.Join(
            "\n",
            method.Method.ToUpperInvariant(),
            canonicalUri,
            canonicalQuery,
            canonicalHeaders.ToString(),
            signedHeaders,
            payloadHash);
        var credentialScope = $"{dateStamp}/{credentials.Region}/{service}/aws4_request";
        var stringToSign = string.Join(
            "\n",
            Algorithm,
            amzDate,
            credentialScope,
            Sha256Hex(Encoding.UTF8.GetBytes(canonicalRequest)));

        var dateKey = HmacSha256(Encoding.UTF8.GetBytes("AWS4" + credentials.SecretAccessKey), dateStamp);
        var regionKey = HmacSha256(dateKey, credentials.Region);
        var serviceKey = HmacSha256(regionKey, service);
        var signingKey = HmacSha256(serviceKey, "aws4_request");
        var signature = Convert.ToHexString(HmacSha256(signingKey, stringToSign)).ToLowerInvariant();
        var authorization =
            $"{Algorithm} Credential={credentials.AccessKeyId}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";

        return new AwsSignatureResult(
            authorization,
            amzDate,
            payloadHash,
            signedHeaders,
            canonicalRequest);
    }

    private static string CanonicalizePath(Uri uri)
    {
        var escapedPath = uri.GetComponents(UriComponents.Path, UriFormat.UriEscaped);
        if (string.IsNullOrEmpty(escapedPath))
            return "/";

        var segments = escapedPath.Split('/');
        return "/" + string.Join('/', segments.Select(segment => Rfc3986Encode(Uri.UnescapeDataString(segment))));
    }

    private static string CanonicalizeQuery(Uri uri)
    {
        var escapedQuery = uri.GetComponents(UriComponents.Query, UriFormat.UriEscaped);
        if (string.IsNullOrEmpty(escapedQuery))
            return string.Empty;

        var pairs = escapedQuery.Split('&', StringSplitOptions.None)
            .Select(part =>
            {
                var separator = part.IndexOf('=');
                var rawName = separator < 0 ? part : part[..separator];
                var rawValue = separator < 0 ? string.Empty : part[(separator + 1)..];
                return (
                    Name: Rfc3986Encode(Uri.UnescapeDataString(rawName)),
                    Value: Rfc3986Encode(Uri.UnescapeDataString(rawValue)));
            })
            .OrderBy(pair => pair.Name, StringComparer.Ordinal)
            .ThenBy(pair => pair.Value, StringComparer.Ordinal);

        return string.Join('&', pairs.Select(pair => $"{pair.Name}={pair.Value}"));
    }

    private static string Rfc3986Encode(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var encoded = new StringBuilder(bytes.Length);
        foreach (var valueByte in bytes)
        {
            if ((valueByte >= (byte)'A' && valueByte <= (byte)'Z') ||
                (valueByte >= (byte)'a' && valueByte <= (byte)'z') ||
                (valueByte >= (byte)'0' && valueByte <= (byte)'9') ||
                valueByte is (byte)'-' or (byte)'_' or (byte)'.' or (byte)'~')
            {
                encoded.Append((char)valueByte);
            }
            else
            {
                encoded.Append('%').Append(valueByte.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return encoded.ToString();
    }

    private static string Sha256Hex(ReadOnlySpan<byte> value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private static byte[] HmacSha256(byte[] key, string value) =>
        HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(value));
}
