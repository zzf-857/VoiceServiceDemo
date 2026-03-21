using System.IO;
using System.Text.Json;
using VoiceServiceDemo.Models;

namespace VoiceServiceDemo.Services;

/// <summary>
/// 本地安全凭证存储服务，在 %APPDATA%\VoiceOps 目录下保存用户的 API Keys 和偏好设置
/// </summary>
public class SettingsService
{
    private static readonly string AppFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VoiceOps");

    private static readonly string ConfigPath = Path.Combine(AppFolder, "config.json");

    private AppSettings _settings = new();

    public AppSettings Settings => _settings;

    public SettingsService()
    {
        Load();
    }

    /// <summary>从本地配置文件加载</summary>
    public void Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            _settings = new AppSettings();
        }

        // 确保输出目录存在
        if (string.IsNullOrEmpty(_settings.OutputDirectory))
        {
            _settings.OutputDirectory = Path.Combine(AppFolder, "output");
        }
        Directory.CreateDirectory(_settings.OutputDirectory);
    }

    /// <summary>保存当前配置至本地文件</summary>
    public void Save()
    {
        Directory.CreateDirectory(AppFolder);
        var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    /// <summary>获取某厂商的 API Key</summary>
    public string GetApiKey(string vendorId)
    {
        return _settings.ApiKeys.TryGetValue(vendorId, out var key) ? key : "";
    }

    /// <summary>设置某厂商的 API Key</summary>
    public void SetApiKey(string vendorId, string apiKey)
    {
        _settings.ApiKeys[vendorId] = apiKey;
        Save();
    }
}
