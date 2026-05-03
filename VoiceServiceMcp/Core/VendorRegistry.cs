namespace VoiceServiceMcp.Core;

/// <summary>
/// 内置的厂商注册表
/// </summary>
public static class VendorRegistry
{
    public static List<VendorConfig> All { get; } = new()
    {
        new VendorConfig
        {
            Id = "huoshan",
            Name = "火山引擎",
            Description = "字节跳动旗下的大模型语音合成服务",
            ApiBaseUrl = "https://openspeech.bytedance.com/api/v3/tts/unidirectional",
            DocumentationUrl = "https://www.volcengine.com/docs/6561/1257536",
            SupportsVoiceFetch = true,
            DefaultModels = new()
            {
                new VoiceModel { Id = "seed-tts-2.0", Name = "豆包语音合成模型 2.0" },
                new VoiceModel { Id = "seed-tts-1.0", Name = "豆包语音合成模型 1.0" },
                new VoiceModel { Id = "legacy-v1", Name = "旧版 /api/v1/tts 兼容" }
            },
            DefaultVoices = new()
            {
                new VoiceOption { Id = "zh_female_wenrouxiaoya_moon_bigtts", Name = "温柔小雅", Gender = "女", Language = "中文", IsBigTTS = true },
                new VoiceOption { Id = "zh_female_tianmeixiaoyuan_moon_bigtts", Name = "甜美小源", Gender = "女", Language = "中文", IsBigTTS = true },
                new VoiceOption { Id = "zh_male_dongfanghaoran_moon_bigtts", Name = "东方浩然", Gender = "男", Language = "中文", IsBigTTS = true },
                new VoiceOption { Id = "zh_male_jieshuoxiaoming_moon_bigtts", Name = "解说小明", Gender = "男", Language = "中文", IsBigTTS = true },
            }
        },
        new VendorConfig
        {
            Id = "openai",
            Name = "OpenAI TTS",
            Description = "自然流畅的语音合成模型",
            ApiBaseUrl = "https://api.openai.com/v1/audio/speech",
            DocumentationUrl = "https://platform.openai.com/docs/guides/text-to-speech",
            DefaultModels = new()
            {
                new VoiceModel { Id = "tts-1", Name = "TTS-1 (快速)" },
                new VoiceModel { Id = "tts-1-hd", Name = "TTS-1-HD (高清)" }
            },
            DefaultVoices = new()
            {
                new VoiceOption { Id = "alloy", Name = "Alloy", Gender = "中性", Language = "多语言" },
                new VoiceOption { Id = "echo", Name = "Echo", Gender = "男", Language = "多语言" },
                new VoiceOption { Id = "nova", Name = "Nova", Gender = "女", Language = "多语言" },
            }
        }
    };

    public static VendorConfig? GetById(string id) => All.FirstOrDefault(v => v.Id == id);
}
