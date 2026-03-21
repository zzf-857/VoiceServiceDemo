namespace VoiceServiceDemo.Models;

/// <summary>
/// 厂商的预设配置（内置，只读）
/// </summary>
public class VendorConfig
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string IconName { get; set; } = "mic"; // lucide icon name
    public string ApiBaseUrl { get; set; } = "";
    public string DocumentationUrl { get; set; } = "";
    public List<VoiceModel> DefaultModels { get; set; } = new();
    public List<VoiceOption> DefaultVoices { get; set; } = new();
    public bool SupportsModelFetch { get; set; } = false;
    public bool SupportsVoiceFetch { get; set; } = false;

    /// <summary>
    /// 厂商特有的语速参数定义
    /// </summary>
    public TtsParameterDef SpeedDef { get; set; } = new();

    /// <summary>
    /// 厂商特有的音量参数定义
    /// </summary>
    public TtsParameterDef VolumeDef { get; set; } = new();
}

/// <summary>
/// TTS 发音参数的阈值和默认值定义
/// </summary>
public class TtsParameterDef
{
    public bool IsSupported { get; set; } = true;
    public double Min { get; set; } = 0.5;
    public double Max { get; set; } = 2.0;
    public double Default { get; set; } = 1.0;
    public double Step { get; set; } = 0.1;
}

/// <summary>
/// 语音模型
/// </summary>
public class VoiceModel
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

/// <summary>
/// 音色选项
/// </summary>
public class VoiceOption
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Gender { get; set; }
    public string? Language { get; set; }
    public string? SampleUrl { get; set; }
    public string? Age { get; set; }
    public List<string> Categories { get; set; } = new();
    public List<EmotionInfo> Emotions { get; set; } = new();
    
    /// <summary>
    /// 是否为 BigTTS 大模型音色（仅火山引擎）
    /// </summary>
    public bool IsBigTTS { get; set; } = false;
}

/// <summary>
/// 情感信息（音色支持的情感变体）
/// </summary>
public class EmotionInfo
{
    public string Emotion { get; set; } = "";       // 中文名，如 "开心"
    public string EmotionType { get; set; } = "";   // 英文键，如 "happy"
    public string? DemoText { get; set; }
    public string? DemoUrl { get; set; }
}

/// <summary>
/// 语音生成请求
/// </summary>
public class TtsRequest
{
    public string VendorId { get; set; } = "";
    public string ModelId { get; set; } = "";
    public string VoiceId { get; set; } = "";
    public string Text { get; set; } = "";
    public double Speed { get; set; } = 1.0;
}

/// <summary>
/// 语音生成结果
/// </summary>
public class TtsResult
{
    public bool Success { get; set; }
    public string? FilePath { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
    public string VendorName { get; set; } = "";
    public string VoiceName { get; set; } = "";
    public string ModelName { get; set; } = "";
    public string Text { get; set; } = "";
    public double DurationSeconds { get; set; }
}

/// <summary>
/// 用户存储的设置
/// </summary>
public class AppSettings
{
    public Dictionary<string, string> ApiKeys { get; set; } = new();
    public string OutputDirectory { get; set; } = "";
}
