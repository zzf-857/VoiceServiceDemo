using VoiceServiceDemo.Models;

namespace VoiceServiceDemo.Services;

public static class HuoshanEmotionPolicy
{
    private const string CatalogMultiEmotionTag = "多情感";

    private static readonly List<TtsExpressionOption> FallbackEmotions = new()
    {
        new TtsExpressionOption { Id = "neutral", Name = "中性" },
        new TtsExpressionOption { Id = "happy", Name = "开心" },
        new TtsExpressionOption { Id = "sad", Name = "悲伤" },
        new TtsExpressionOption { Id = "angry", Name = "生气" },
        new TtsExpressionOption { Id = "surprised", Name = "惊讶" },
        new TtsExpressionOption { Id = "fear", Name = "恐惧" },
        new TtsExpressionOption { Id = "hate", Name = "厌恶" },
        new TtsExpressionOption { Id = "excited", Name = "激动" },
        new TtsExpressionOption { Id = "tender", Name = "温柔" },
        new TtsExpressionOption { Id = "storytelling", Name = "讲故事" }
    };

    public static IEnumerable<TtsExpressionOption> GetOptions(VoiceOption voice)
    {
        if (voice.Emotions.Any())
        {
            return voice.Emotions
                .Where(e => !string.IsNullOrWhiteSpace(e.EmotionType))
                .Select(e => new TtsExpressionOption
                {
                    Id = e.EmotionType,
                    Name = string.IsNullOrWhiteSpace(e.Emotion) ? e.EmotionType : e.Emotion
                })
                .GroupBy(e => e.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First());
        }

        if (IsFetchedVoice(voice))
            return Enumerable.Empty<TtsExpressionOption>();

        if (LooksLikeExpressiveBigTtsVoice(voice))
            return FallbackEmotions;

        return Enumerable.Empty<TtsExpressionOption>();
    }

    public static bool HasSelectableEmotionControls(VoiceOption voice) =>
        GetOptions(voice)
            .Select(e => e.Id)
            .Where(id => !IsDefaultEmotion(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Any();

    public static IEnumerable<string> GetDisplayTags(VoiceOption voice)
    {
        foreach (var category in voice.Categories.Where(c => !IsCatalogOnlyEmotionTag(c)).Take(2))
            yield return category;

        var optionCount = GetOptions(voice).Count();
        if (HasSelectableEmotionControls(voice) && optionCount > 1)
            yield return $"{optionCount} 种语气";
    }

    public static bool MatchesCategory(VoiceOption voice, string category)
    {
        if (IsCatalogOnlyEmotionTag(category))
            return HasSelectableEmotionControls(voice);

        return voice.Categories.Contains(category);
    }

    public static string GetResourceId(VoiceOption voice) =>
        voice.Categories.FirstOrDefault(IsResourceCategory) ?? "";

    public static string ToRequestEmotion(string emotionId)
    {
        var normalized = emotionId.Trim();
        return IsDefaultEmotion(normalized) ? "" : normalized;
    }

    private static bool IsFetchedVoice(VoiceOption voice) =>
        !string.IsNullOrWhiteSpace(voice.SampleUrl) || voice.Categories.Count > 0;

    private static bool LooksLikeExpressiveBigTtsVoice(VoiceOption voice) =>
        voice.IsBigTTS ||
        voice.Id.Contains("_bigtts", StringComparison.OrdinalIgnoreCase) ||
        voice.Id.Contains("_moon_", StringComparison.OrdinalIgnoreCase) ||
        voice.Id.Contains("_mars_", StringComparison.OrdinalIgnoreCase) ||
        voice.Id.StartsWith("BV", StringComparison.OrdinalIgnoreCase);

    private static bool IsCatalogOnlyEmotionTag(string category) =>
        string.Equals(category, CatalogMultiEmotionTag, StringComparison.OrdinalIgnoreCase);

    private static bool IsDefaultEmotion(string emotionId) =>
        string.IsNullOrWhiteSpace(emotionId) ||
        string.Equals(emotionId, "general", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(emotionId, "neutral", StringComparison.OrdinalIgnoreCase);

    private static bool IsResourceCategory(string category) =>
        category.StartsWith("seed-", StringComparison.OrdinalIgnoreCase);
}
