using System.Globalization;
using System.Security;
using VoiceServiceDemo.Models;

namespace VoiceServiceDemo.Services;

public static class AzureSsmlBuilder
{
    public static string Build(TtsRequest request)
    {
        if (request.InputFormat == TtsInputFormat.Ssml && !string.IsNullOrWhiteSpace(request.SsmlText))
            return request.SsmlText.Trim();

        var text = SecurityElement.Escape(request.Text) ?? "";
        var rate = request.Speed.ToString("0.00", CultureInfo.InvariantCulture);
        var volume = request.Volume.ToString("0.00", CultureInfo.InvariantCulture);
        var styleDegree = request.StyleDegree.ToString("0.0", CultureInfo.InvariantCulture);
        var voice = SecurityElement.Escape(request.VoiceId) ?? "";

        var inner = $"<prosody rate=\"{rate}\" volume=\"{volume}\">{text}</prosody>";
        if (!string.IsNullOrWhiteSpace(request.Style))
        {
            var style = SecurityElement.Escape(request.Style.Trim()) ?? "";
            var role = string.IsNullOrWhiteSpace(request.Role)
                ? ""
                : $" role=\"{SecurityElement.Escape(request.Role.Trim())}\"";
            inner = $"<mstts:express-as style=\"{style}\" styledegree=\"{styleDegree}\"{role}>{inner}</mstts:express-as>";
        }

        return $"""
<speak version="1.0" xmlns="http://www.w3.org/2001/10/synthesis" xmlns:mstts="https://www.w3.org/2001/mstts" xml:lang="zh-CN">
  <voice name="{voice}">
    {inner}
  </voice>
</speak>
""";
    }
}
