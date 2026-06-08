using VoiceServiceDemo.Models;

namespace VoiceServiceDemo.Services;

public static class TencentEmotionPolicy
{
    public const int MinIntensity = 50;
    public const int MaxIntensity = 200;
    public const int DefaultIntensity = 100;

    private static readonly IReadOnlyList<TtsExpressionOption> Options = new List<TtsExpressionOption>
    {
        new() { Id = "", Name = "不指定" },
        new() { Id = "neutral", Name = "中性" },
        new() { Id = "sad", Name = "悲伤" },
        new() { Id = "happy", Name = "高兴" },
        new() { Id = "angry", Name = "生气" },
        new() { Id = "fear", Name = "恐惧" },
        new() { Id = "news", Name = "新闻" },
        new() { Id = "story", Name = "故事" },
        new() { Id = "radio", Name = "广播" },
        new() { Id = "poetry", Name = "诗歌" },
        new() { Id = "call", Name = "客服" },
        new() { Id = "sajiao", Name = "撒娇" },
        new() { Id = "disgusted", Name = "厌恶" },
        new() { Id = "amaze", Name = "震惊" },
        new() { Id = "peaceful", Name = "平静" },
        new() { Id = "exciting", Name = "兴奋" },
        new() { Id = "aojiao", Name = "傲娇" },
        new() { Id = "jieshuo", Name = "解说" }
    };

    private static readonly HashSet<string> SupportedCategories = Options
        .Select(option => option.Id)
        .Where(id => !string.IsNullOrWhiteSpace(id))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<TtsExpressionOption> GetOptions() => Options;

    public static string ToRequestEmotion(string? emotion)
    {
        var normalized = (emotion ?? "").Trim();
        return SupportedCategories.Contains(normalized) ? normalized.ToLowerInvariant() : "";
    }

    public static int ClampIntensity(int intensity) => Math.Clamp(intensity, MinIntensity, MaxIntensity);
}
