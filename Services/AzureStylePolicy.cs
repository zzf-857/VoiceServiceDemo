using VoiceServiceDemo.Models;

namespace VoiceServiceDemo.Services;

public static class AzureStylePolicy
{
    private static readonly IReadOnlyDictionary<string, string> KnownStyles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [""] = "默认",
        ["cheerful"] = "愉快",
        ["sad"] = "悲伤",
        ["angry"] = "生气",
        ["chat"] = "聊天",
        ["customerservice"] = "客服",
        ["newscast"] = "新闻"
    };

    public static IReadOnlyList<TtsExpressionOption> GetOptions(VoiceOption? voice)
    {
        var styleIds = voice?.Categories
            .Where(category => category.StartsWith("style:", StringComparison.OrdinalIgnoreCase))
            .Select(category => category["style:".Length..].Trim())
            .Where(style => !string.IsNullOrWhiteSpace(style))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        if (styleIds.Count == 0)
            return GetFallbackOptions();

        var options = new List<TtsExpressionOption> { new() { Id = "", Name = "默认" } };
        options.AddRange(styleIds.Select(style => new TtsExpressionOption
        {
            Id = style,
            Name = KnownStyles.TryGetValue(style, out var label) ? label : style
        }));

        return options;
    }

    private static IReadOnlyList<TtsExpressionOption> GetFallbackOptions() => KnownStyles
        .Select(pair => new TtsExpressionOption { Id = pair.Key, Name = pair.Value })
        .ToList();
}
