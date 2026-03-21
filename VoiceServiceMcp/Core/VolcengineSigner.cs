using System.Security.Cryptography;
using System.Text;

namespace VoiceServiceMcp.Core;

public static class VolcengineSigner
{
    /// <summary>
    /// 对 HttpRequestMessage 附加火山引擎 OpenAPI V4 签名
    /// </summary>
    public static void SignRequest(HttpRequestMessage request, string ak, string sk, string region, string service, byte[] bodyBytes)
    {
        var method = request.Method.Method;
        var uri = request.RequestUri!;
        var date = DateTime.UtcNow;
        var dateStr = date.ToString("yyyyMMdd'T'HHmmss'Z'");
        var shortDate = date.ToString("yyyyMMdd");

        using var sha256 = SHA256.Create();
        var hashedPayload = ToHexString(sha256.ComputeHash(bodyBytes));

        var queryString = uri.Query.TrimStart('?');
        if (!string.IsNullOrEmpty(queryString))
        {
            var pairs = queryString.Split('&')
                .Select(p => p.Split(new[] { '=' }, 2))
                .OrderBy(p => p[0], StringComparer.Ordinal)
                .Select(p => p.Length == 2 ? $"{p[0]}={p[1]}" : p[0]);
            queryString = string.Join("&", pairs);
        }

        var host = uri.Host;
        var contentType = "application/json; charset=UTF-8";

        var canonicalHeaders =
            $"content-type:{contentType}\n" +
            $"host:{host}\n" +
            $"x-content-sha256:{hashedPayload}\n" +
            $"x-date:{dateStr}\n";
        var signedHeaders = "content-type;host;x-content-sha256;x-date";

        var canonicalRequest = $"{method}\n/\n{queryString}\n{canonicalHeaders}\n{signedHeaders}\n{hashedPayload}";
        var canonicalRequestHash = ToHexString(sha256.ComputeHash(Encoding.UTF8.GetBytes(canonicalRequest)));

        var credentialScope = $"{shortDate}/{region}/{service}/request";
        var stringToSign = $"HMAC-SHA256\n{dateStr}\n{credentialScope}\n{canonicalRequestHash}";

        var kDate = HmacSha256(Encoding.UTF8.GetBytes(sk), shortDate);
        var kRegion = HmacSha256(kDate, region);
        var kService = HmacSha256(kRegion, service);
        var kSigning = HmacSha256(kService, "request");
        var signature = ToHexString(HmacSha256(kSigning, stringToSign));

        request.Headers.Remove("X-Date");
        request.Headers.Remove("X-Content-Sha256");
        request.Headers.Remove("Authorization");

        request.Headers.Add("X-Date", dateStr);
        request.Headers.Add("X-Content-Sha256", hashedPayload);
        var authHeader = $"HMAC-SHA256 Credential={ak}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";
        request.Headers.TryAddWithoutValidation("Authorization", authHeader);
    }

    private static byte[] HmacSha256(byte[] key, string data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    private static string ToHexString(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
            sb.AppendFormat("{0:x2}", b);
        return sb.ToString();
    }
}
