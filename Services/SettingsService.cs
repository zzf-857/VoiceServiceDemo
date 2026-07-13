using System.IO;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using VoiceServiceDemo.Models;
using VoiceServiceLocalApi;

namespace VoiceServiceDemo.Services;

/// <summary>
/// 本地安全凭证存储服务，在 %APPDATA%\VoiceOps 目录下保存用户的 API Keys 和偏好设置
/// </summary>
public class SettingsService
{
    private static readonly string DefaultAppFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VoiceOps");

    private static readonly string DefaultConfigPath = Path.Combine(DefaultAppFolder, "config.json");

    private readonly string _appFolder;
    private readonly string _configPath;
    private readonly object _sync = new();
    private AppSettings _settings = new();

    public AppSettings Settings => _settings;

    [ActivatorUtilitiesConstructor]
    public SettingsService()
        : this(DefaultConfigPath)
    {
    }

    public SettingsService(string configPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);
        _configPath = Path.GetFullPath(configPath);
        _appFolder = Path.GetDirectoryName(_configPath)
            ?? throw new ArgumentException("The settings path must have a parent directory.", nameof(configPath));
        Load();
    }

    /// <summary>从本地配置文件加载</summary>
    public void Load()
    {
        var shouldSave = false;
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            else
            {
                shouldSave = true;
            }
        }
        catch
        {
            _settings = new AppSettings();
            shouldSave = true;
        }

        if (_settings.ApiKeys == null)
        {
            _settings.ApiKeys = new();
            shouldSave = true;
        }

        // 确保输出目录存在
        if (string.IsNullOrEmpty(_settings.OutputDirectory))
        {
            _settings.OutputDirectory = Path.Combine(_appFolder, "output");
            shouldSave = true;
        }
        Directory.CreateDirectory(_settings.OutputDirectory);

        if (_settings.LocalApi == null)
        {
            _settings.LocalApi = new LocalApiSettings();
            shouldSave = true;
        }

        var localApi = _settings.LocalApi;
        if (localApi.Port is < 1024 or > 65_535)
        {
            localApi.Port = 5055;
            shouldSave = true;
        }
        if (localApi.MaxConcurrentRequests <= 0)
        {
            localApi.MaxConcurrentRequests = 2;
            shouldSave = true;
        }
        if (localApi.MaxTextLength is < 1 or > 20_000)
        {
            localApi.MaxTextLength = 20_000;
            shouldSave = true;
        }
        if (string.IsNullOrWhiteSpace(localApi.AccessToken))
        {
            localApi.AccessToken = LocalApiToken.Generate();
            shouldSave = true;
        }

        if (shouldSave)
            Save();
    }

    /// <summary>保存当前配置至本地文件</summary>
    public void Save()
    {
        lock (_sync)
        {
            Directory.CreateDirectory(_appFolder);
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
        }
    }

    /// <summary>获取某厂商的 API Key</summary>
    public string GetApiKey(string vendorId)
    {
        lock (_sync)
            return _settings.ApiKeys.TryGetValue(vendorId, out var key) ? key : "";
    }

    /// <summary>设置某厂商的 API Key</summary>
    public void SetApiKey(string vendorId, string apiKey)
    {
        lock (_sync)
        {
            _settings.ApiKeys[vendorId] = apiKey;
            Save();
        }
    }
}
