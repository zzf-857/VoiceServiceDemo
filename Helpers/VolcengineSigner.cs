using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Net.Http;
using System.Net.Http.Headers;

namespace VoiceServiceDemo.Helpers;

public static class VolcengineSigner
{
    /// <summary>
    /// 对 HttpRequestMessage 附加火山引擎 OpenAPI V4 签名
    /// content-type 必须与实际请求头完全一致（区分大小写）
    /// </summary>
    public static void SignRequest(HttpRequestMessage request, string ak, string sk, string region, string service, byte[] bodyBytes)
    {
        var method = request.Method.Method;
        var uri = request.RequestUri!;
        var date = DateTime.UtcNow;
        var dateStr = date.ToString("yyyyMMdd'T'HHmmss'Z'");
        var shortDate = date.ToString("yyyyMMdd");

        // 1) 计算 body hash
        using var sha256 = SHA256.Create();
        var hashedPayload = ToHexString(sha256.ComputeHash(bodyBytes));

        // 2) 规范化 query string（按 key 字典序排列）
        var queryString = uri.Query.TrimStart('?');
        if (!string.IsNullOrEmpty(queryString))
        {
            var pairs = queryString.Split('&')
                .Select(p => p.Split(new[] { '=' }, 2))
                .OrderBy(p => p[0], StringComparer.Ordinal)
                .Select(p => p.Length == 2 ? $"{p[0]}={p[1]}" : p[0]);
            queryString = string.Join("&", pairs);
        }

        // 3) 规范化 headers（签名要求 header 名全部小写，值 trim）
        var host = uri.Host;
        // content-type 必须与实际设置值完全一致
        var contentType = "application/json; charset=UTF-8";

        var canonicalHeaders =
            $"content-type:{contentType}\n" +
            $"host:{host}\n" +
            $"x-content-sha256:{hashedPayload}\n" +
            $"x-date:{dateStr}\n";
        var signedHeaders = "content-type;host;x-content-sha256;x-date";

        // 4) Canonical Request
        var canonicalRequest = $"{method}\n/\n{queryString}\n{canonicalHeaders}\n{signedHeaders}\n{hashedPayload}";
        var canonicalRequestHash = ToHexString(sha256.ComputeHash(Encoding.UTF8.GetBytes(canonicalRequest)));

        // 5) String to Sign
        var credentialScope = $"{shortDate}/{region}/{service}/request";
        var stringToSign = $"HMAC-SHA256\n{dateStr}\n{credentialScope}\n{canonicalRequestHash}";

        // 6) 派生签名密钥
        var kDate = HmacSha256(Encoding.UTF8.GetBytes(sk), shortDate);
        var kRegion = HmacSha256(kDate, region);
        var kService = HmacSha256(kRegion, service);
        var kSigning = HmacSha256(kService, "request");
        var signature = ToHexString(HmacSha256(kSigning, stringToSign));

        // 7) 添加请求头（注意不要重复添加）
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
