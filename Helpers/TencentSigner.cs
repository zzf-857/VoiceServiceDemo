using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Net.Http;
using System.Net.Http.Headers;

namespace VoiceServiceDemo.Helpers;

/// <summary>
/// 腾讯云 TC3-HMAC-SHA256 签名工具
/// 严格按照官方文档 C# 示例实现：
/// https://cloud.tencent.com/document/api/1073/37990
/// </summary>
public static class TencentSigner
{
    /// <summary>
    /// 对 HttpRequestMessage 附加腾讯云 TC3-HMAC-SHA256 签名。
    /// 调用前需先设置 X-TC-Action header，Content 由本方法设置。
    /// </summary>
    public static void SignRequest(HttpRequestMessage request, string secretId, string secretKey, string service, byte[] requestPayload)
    {
        string endpoint = request.RequestUri!.Host;

        // 从请求头中读取 action（调用前必须已设置 X-TC-Action）
        string action = request.Headers.GetValues("X-TC-Action").First();

        // ── 时间戳（与官方示例一致：UTC 时间）──
        DateTime date = DateTime.UtcNow;
        string datestr = date.ToString("yyyy-MM-dd");
        DateTime startTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        long requestTimestamp = (long)Math.Round((date - startTime).TotalMilliseconds, MidpointRounding.AwayFromZero) / 1000;

        // ************* 步骤 1：拼接规范请求串（与官方示例完全一致）*************
        string algorithm = "TC3-HMAC-SHA256";
        string httpRequestMethod = "POST";
        string canonicalUri = "/";
        string canonicalQueryString = "";
        string contentType = "application/json";

        // 官方示例原文：
        // string canonicalHeaders = "content-type:" + contentType + "; charset=utf-8\n"
        //     + "host:" + endpoint + "\n"
        //     + "x-tc-action:" + action.ToLower() + "\n";
        string canonicalHeaders = "content-type:" + contentType + "; charset=utf-8\n"
            + "host:" + endpoint + "\n"
            + "x-tc-action:" + action.ToLower() + "\n";
        string signedHeaders = "content-type;host;x-tc-action";

        string hashedRequestPayload = SHA256Hex(Encoding.UTF8.GetString(requestPayload));
        string canonicalRequest = httpRequestMethod + "\n"
            + canonicalUri + "\n"
            + canonicalQueryString + "\n"
            + canonicalHeaders + "\n"
            + signedHeaders + "\n"
            + hashedRequestPayload;

        // ************* 步骤 2：拼接待签名字符串 *************
        string credentialScope = datestr + "/" + service + "/" + "tc3_request";
        string hashedCanonicalRequest = SHA256Hex(canonicalRequest);
        string stringToSign = algorithm + "\n"
            + requestTimestamp.ToString() + "\n"
            + credentialScope + "\n"
            + hashedCanonicalRequest;

        // ************* 步骤 3：计算签名 *************
        byte[] tc3SecretKey = Encoding.UTF8.GetBytes("TC3" + secretKey);
        byte[] secretDate = HmacSHA256(tc3SecretKey, Encoding.UTF8.GetBytes(datestr));
        byte[] secretService = HmacSHA256(secretDate, Encoding.UTF8.GetBytes(service));
        byte[] secretSigning = HmacSHA256(secretService, Encoding.UTF8.GetBytes("tc3_request"));
        byte[] signatureBytes = HmacSHA256(secretSigning, Encoding.UTF8.GetBytes(stringToSign));
        string signature = BitConverter.ToString(signatureBytes).Replace("-", "").ToLower();

        // ************* 步骤 4：拼接 Authorization *************
        string authorization = algorithm + " "
            + "Credential=" + secretId + "/" + credentialScope + ", "
            + "SignedHeaders=" + signedHeaders + ", "
            + "Signature=" + signature;

        // ── 设置请求头（与官方示例 BuildHeaders 输出完全一致）──
        // Content-Type 必须与 canonicalHeaders 中的值完全一致
        request.Content = new ByteArrayContent(requestPayload);
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType + "; charset=utf-8");

        request.Headers.Remove("Authorization");
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        request.Headers.Remove("Host");
        request.Headers.TryAddWithoutValidation("Host", endpoint);
        request.Headers.Remove("X-TC-Timestamp");
        request.Headers.Add("X-TC-Timestamp", requestTimestamp.ToString());
    }

    public static string SHA256Hex(string s)
    {
        using (SHA256 algo = SHA256.Create())
        {
            byte[] hashbytes = algo.ComputeHash(Encoding.UTF8.GetBytes(s));
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < hashbytes.Length; ++i)
            {
                builder.Append(hashbytes[i].ToString("x2"));
            }
            return builder.ToString();
        }
    }

    public static byte[] HmacSHA256(byte[] key, byte[] msg)
    {
        using (HMACSHA256 mac = new HMACSHA256(key))
        {
            return mac.ComputeHash(msg);
        }
    }
}
