namespace VoiceServiceDemo.Services;

using VoiceServiceDemo.Models;

/// <summary>
/// 内置的厂商注册表，包含所有预配置的 TTS 服务商信息
/// </summary>
public static class VendorRegistry
{
    public static List<VendorConfig> All { get; } = new()
    {
        // ===================== 中国厂商优先 =====================
        new VendorConfig
        {
            Id = "huoshan",
            Name = "火山引擎",
            Description = "字节跳动旗下的大模型语音合成服务，支持极高自然度的中文语音。",
            IconName = "flame",
            ApiBaseUrl = "https://openspeech.bytedance.com/api/v3/tts/unidirectional",
            DocumentationUrl = "https://www.volcengine.com/docs/6561/1257536",
            ImportantLinks = new()
            {
                new VendorLink
                {
                    Label = "凭证参数获取",
                    Url = "https://www.volcengine.com/docs/6561/196768?lang=zh#q1%EF%BC%9A%E5%93%AA%E9%87%8C%E5%8F%AF%E4%BB%A5%E8%8E%B7%E5%8F%96%E5%88%B0%E4%BB%A5%E4%B8%8B%E5%8F%82%E6%95%B0appid%EF%BC%8Ccluster%EF%BC%8Ctoken%EF%BC%8Cauthorization-type%EF%BC%8Csecret-key-%EF%BC%9F",
                    Description = "AppID、Cluster、Token、Authorization Type、Secret Key 获取说明"
                },
                new VendorLink
                {
                    Label = "TTS 模型实验室",
                    Url = "https://console.volcengine.com/ark/region:ark+cn-beijing/experience/voice?modelId=doubao-seed-tts-2-0&tab=TTS",
                    Description = "打开豆包语音合成 2.0 官方体验页"
                },
                new VendorLink
                {
                    Label = "豆包语音体验",
                    Url = "https://console.volcengine.com/speech/new/overview?projectName=default",
                    Description = "打开豆包语音体验入口"
                }
            },
            Capabilities = new VendorCapabilities
            {
                SupportsEmotion = true
            },
            SpeedDef = new TtsParameterDef { Min = 0.5, Max = 2.0, Default = 1.0, Step = 0.1 },
            VolumeDef = new TtsParameterDef { Min = 0.1, Max = 3.0, Default = 1.0, Step = 0.1 },
            SupportsModelFetch = false,
            SupportsVoiceFetch = true,
            DefaultModels = new()
            {
                new VoiceModel { Id = "seed-tts-2.0", Name = "豆包语音合成模型 2.0" },
                new VoiceModel { Id = "seed-tts-1.0", Name = "豆包语音合成模型 1.0" },
                new VoiceModel { Id = "legacy-v1", Name = "旧版 /api/v1/tts 兼容" }
            },
            DefaultVoices = new()
            {
                new VoiceOption { Id = "zh_female_wenrouxiaoya_moon_bigtts", Name = "温柔小雅 (角色扮演)", Gender = "女", Language = "中文", IsBigTTS = true },
                new VoiceOption { Id = "zh_female_tianmeixiaoyuan_moon_bigtts", Name = "甜美小源 (通用场景)", Gender = "女", Language = "中文", IsBigTTS = true },
                new VoiceOption { Id = "zh_female_qingchezizi_moon_bigtts", Name = "清澈梓梓 (通用场景)", Gender = "女", Language = "中文", IsBigTTS = true },
                new VoiceOption { Id = "zh_male_dongfanghaoran_moon_bigtts", Name = "东方浩然 (角色扮演)", Gender = "男", Language = "中文", IsBigTTS = true },
                new VoiceOption { Id = "zh_male_jieshuoxiaoming_moon_bigtts", Name = "解说小明 (通用场景)", Gender = "男", Language = "中文", IsBigTTS = true },
                new VoiceOption { Id = "zh_female_kailangjiejie_moon_bigtts", Name = "开朗姐姐 (通用场景)", Gender = "女", Language = "中文", IsBigTTS = true },
                new VoiceOption { Id = "zh_male_linjiananhai_moon_bigtts", Name = "邻家男孩 (通用场景)", Gender = "男", Language = "中文", IsBigTTS = true },
                new VoiceOption { Id = "zh_female_tianmeiyueyue_moon_bigtts", Name = "甜美悦悦 (通用场景)", Gender = "女", Language = "中文", IsBigTTS = true },
                new VoiceOption { Id = "zh_female_xinlingjitang_moon_bigtts", Name = "心灵鸡汤 (通用场景)", Gender = "女", Language = "中文", IsBigTTS = true },
                new VoiceOption { Id = "zh_female_cancan_mars_bigtts", Name = "灿灿 (通用场景)", Gender = "女", Language = "中文", IsBigTTS = true },
            }
        },
        new VendorConfig
        {
            Id = "tencent",
            Name = "腾讯云",
            Description = "腾讯云语音合成服务，提供多种高拟真度音色，支持大模型超自然语音。",
            IconName = "message-square",
            ApiBaseUrl = "https://tts.tencentcloudapi.com",
            DocumentationUrl = "https://cloud.tencent.com/document/product/1073",
            Capabilities = new VendorCapabilities
            {
                SupportsEmotion = true,
                SupportedOutputFormats = new() { "mp3", "wav", "pcm" }
            },
            SpeedDef = new TtsParameterDef { Min = -2, Max = 2, Default = 0, Step = 1 }, // 腾讯音量语速都采用整数范围
            VolumeDef = new TtsParameterDef { Min = 0, Max = 10, Default = 0, Step = 1 },
            SupportsModelFetch = false,
            SupportsVoiceFetch = true,
            DefaultModels = new()
            {
                new VoiceModel { Id = "tencent_standard", Name = "腾讯云 标准版" },
                new VoiceModel { Id = "tencent_bigmodel", Name = "腾讯云 大模型版" }
            },
            DefaultVoices = new()
            {
                new VoiceOption { Id = "502001", Name = "智小柔 (温柔亲和)", Gender = "女", Language = "中文", SampleUrl = "https://tts-tone-audio-1300466766.cos.ap-shanghai.myqcloud.com/%E6%99%BA%E5%B0%8F%E6%9F%94.wav" },
                new VoiceOption { Id = "502006", Name = "智小悟 (阳光男声)", Gender = "男", Language = "中文", SampleUrl = "https://tts-tone-audio-1300466766.cos.ap-shanghai.myqcloud.com/502006.wav" },
                new VoiceOption { Id = "502003", Name = "智小敏 (活力女声)", Gender = "女", Language = "中文", SampleUrl = "https://tts-tone-audio-1300466766.cos.ap-shanghai.myqcloud.com/502003.mp3" },
                new VoiceOption { Id = "603006", Name = "沉稳青叔 (沉稳磁性)", Gender = "男", Language = "中文", SampleUrl = "https://tts-tone-audio-1300466766.cos.ap-shanghai.myqcloud.com/603006.wav" },
                new VoiceOption { Id = "603007", Name = "邻家女孩 (亲切自然)", Gender = "女", Language = "中文", SampleUrl = "https://tts-tone-audio-1300466766.cos.ap-shanghai.myqcloud.com/603007.wav" },
                new VoiceOption { Id = "502005", Name = "智小解 (解说男声)", Gender = "男", Language = "中文", SampleUrl = "https://tts-tone-audio-1300466766.cos.ap-shanghai.myqcloud.com/502005.wav" },
            }
        },
        new VendorConfig
        {
            Id = "aliyun",
            Name = "阿里云 CosyVoice",
            Description = "百炼平台的大模型语音合成服务，支持 CosyVoice 系列。",
            IconName = "cloud",
            ApiBaseUrl = "https://dashscope.aliyuncs.com/api/v1/services/aigc/text2audio/generation",
            DocumentationUrl = "https://help.aliyun.com/zh/model-studio/developer-reference/cosyvoice",
            Capabilities = new VendorCapabilities
            {
                SupportsInstructions = true
            },
            SpeedDef = new TtsParameterDef { Min = 0.5, Max = 2.0, Default = 1.0, Step = 0.1 },
            VolumeDef = new TtsParameterDef { IsSupported = false }, // 暂时不支持直接调音量
            SupportsModelFetch = false,
            SupportsVoiceFetch = true,
            DefaultModels = new()
            {
                new VoiceModel { Id = "qwen3-tts-instruct-flash-2026-01-26", Name = "千问3-TTS-Instruct-Flash (2026-01-26)" }
            },
            DefaultVoices = new()
            {
                // 音色列表将通过主动获取（JSON）动态加载
            }
        },
        new VendorConfig
        {
            Id = "baidu",
            Name = "百度智能云",
            Description = "百度短文本在线合成服务，稳定成熟的中文 TTS 引擎。",
            IconName = "scan-search",
            ApiBaseUrl = "https://tsn.baidu.com/text2audio",
            DocumentationUrl = "https://ai.baidu.com/ai-doc/SPEECH/Jlbxdezuf",
            SpeedDef = new TtsParameterDef { Min = 0, Max = 15, Default = 5, Step = 1 },
            VolumeDef = new TtsParameterDef { Min = 0, Max = 15, Default = 5, Step = 1 },
            SupportsModelFetch = false,
            SupportsVoiceFetch = false,
            DefaultModels = new()
            {
                new VoiceModel { Id = "baidu_standard", Name = "百度标准合成" },
                new VoiceModel { Id = "baidu_premium", Name = "百度精品合成" }
            },
            DefaultVoices = new()
            {
                new VoiceOption { Id = "0", Name = "女声 (标准)", Gender = "女", Language = "中文" },
                new VoiceOption { Id = "1", Name = "男声 (标准)", Gender = "男", Language = "中文" },
                new VoiceOption { Id = "3", Name = "情感女声", Gender = "女", Language = "中文" },
                new VoiceOption { Id = "4", Name = "情感男声", Gender = "男", Language = "中文" },
            }
        },
        // ===================== 海外厂商 =====================
        new VendorConfig
        {
            Id = "azure",
            Name = "Microsoft Azure",
            Description = "全球领先的多语言、多角色神经元语音合成服务。",
            IconName = "layout-template",
            ApiBaseUrl = "https://{region}.tts.speech.microsoft.com/cognitiveservices/v1",
            DocumentationUrl = "https://learn.microsoft.com/zh-cn/azure/ai-services/speech-service/rest-text-to-speech",
            Capabilities = new VendorCapabilities
            {
                SupportsSsml = true,
                SupportsStyle = true,
                SupportsStyleDegree = true,
                SupportedInputFormats = new() { TtsInputFormat.PlainText, TtsInputFormat.Ssml },
                SupportedOutputFormats = new() { "mp3_16k", "mp3_24k", "riff_16k_pcm", "riff_24k_pcm", "raw_16k_pcm", "ogg_24k_opus" }
            },
            SpeedDef = new TtsParameterDef { Min = 0.1, Max = 3.0, Default = 1.0, Step = 0.1 },
            VolumeDef = new TtsParameterDef { Min = 0.0, Max = 100.0, Default = 100.0, Step = 1 }, // 以百分比映射
            SupportsModelFetch = false,
            SupportsVoiceFetch = true,
            DefaultModels = new()
            {
                new VoiceModel { Id = "neural", Name = "Neural TTS" }
            },
            DefaultVoices = new()
            {
                new VoiceOption { Id = "zh-CN-XiaoxiaoNeural", Name = "晓晓 (女)", Gender = "女", Language = "中文" },
                new VoiceOption { Id = "zh-CN-YunxiNeural", Name = "云希 (男)", Gender = "男", Language = "中文" },
                new VoiceOption { Id = "en-US-JennyNeural", Name = "Jenny (女)", Gender = "Female", Language = "英文" },
            }
        },
        new VendorConfig
        {
            Id = "google",
            Name = "Google TTS",
            Description = "Google 广泛覆盖的跨语言文字转语音引擎。",
            IconName = "globe",
            ApiBaseUrl = "https://texttospeech.googleapis.com/v1/text:synthesize",
            DocumentationUrl = "https://cloud.google.com/text-to-speech/docs",
            Capabilities = new VendorCapabilities
            {
                SupportsSsml = true,
                SupportedInputFormats = new() { TtsInputFormat.PlainText, TtsInputFormat.Ssml },
                SupportedOutputFormats = new() { "mp3", "linear16", "ogg_opus", "mulaw", "alaw" }
            },
            SpeedDef = new TtsParameterDef { Min = 0.25, Max = 4.0, Default = 1.0, Step = 0.05 },
            VolumeDef = new TtsParameterDef { Min = -96, Max = 16, Default = 0, Step = 1 }, // gain DB
            SupportsModelFetch = false,
            SupportsVoiceFetch = true,
            DefaultModels = new()
            {
                new VoiceModel { Id = "standard", Name = "Standard" },
                new VoiceModel { Id = "wavenet", Name = "WaveNet" },
                new VoiceModel { Id = "neural2", Name = "Neural2" }
            },
            DefaultVoices = new()
            {
                new VoiceOption { Id = "cmn-CN-Wavenet-A", Name = "中文女声 A", Gender = "女", Language = "中文" },
                new VoiceOption { Id = "cmn-CN-Wavenet-B", Name = "中文男声 B", Gender = "男", Language = "中文" },
                new VoiceOption { Id = "en-US-Wavenet-D", Name = "英文男声 D", Gender = "Male", Language = "英文" },
            }
        },
        new VendorConfig
        {
            Id = "openai",
            Name = "OpenAI TTS",
            Description = "自然流畅且最具逼真情感表达的语音合成模型。",
            IconName = "bot",
            ApiBaseUrl = "https://api.openai.com/v1/audio/speech",
            DocumentationUrl = "https://platform.openai.com/docs/guides/text-to-speech",
            Capabilities = new VendorCapabilities
            {
                SupportsInstructions = true,
                SupportedOutputFormats = new() { "mp3", "opus", "aac", "flac", "wav", "pcm" }
            },
            SpeedDef = new TtsParameterDef { Min = 0.25, Max = 4.0, Default = 1.0, Step = 0.05 },
            VolumeDef = new TtsParameterDef { IsSupported = false },
            SupportsModelFetch = false,
            SupportsVoiceFetch = false,
            DefaultModels = new()
            {
                new VoiceModel { Id = "tts-1", Name = "TTS-1 (快速)" },
                new VoiceModel { Id = "tts-1-hd", Name = "TTS-1-HD (高清)" },
                new VoiceModel { Id = "gpt-4o-mini-tts", Name = "GPT-4o Mini TTS" }
            },
            DefaultVoices = new()
            {
                new VoiceOption { Id = "alloy", Name = "Alloy (合金)", Gender = "中性", Language = "多语言" },
                new VoiceOption { Id = "echo", Name = "Echo (回声)", Gender = "男", Language = "多语言" },
                new VoiceOption { Id = "fable", Name = "Fable (寓言)", Gender = "中性", Language = "多语言" },
                new VoiceOption { Id = "onyx", Name = "Onyx (奥尼克斯)", Gender = "男", Language = "多语言" },
                new VoiceOption { Id = "nova", Name = "Nova (新星)", Gender = "女", Language = "多语言" },
                new VoiceOption { Id = "shimmer", Name = "Shimmer (微光)", Gender = "女", Language = "多语言" },
            }
        },
    };

    public static VendorConfig? GetById(string id) => All.FirstOrDefault(v => v.Id == id);
}
