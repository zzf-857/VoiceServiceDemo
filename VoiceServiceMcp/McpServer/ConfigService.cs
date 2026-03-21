namespace VoiceServiceMcp.McpServer;

/// <summary>
/// 配置服务 - 从环境变量读取 API Keys
/// </summary>
public class ConfigService
{
    private readonly Dictionary<string, string> _apiKeys = new();

    public ConfigService()
    {
        // 从环境变量加载 API Keys
        LoadApiKey("huoshan", "HUOSHAN_API_KEY");
        LoadApiKey("openai", "OPENAI_API_KEY");
        LoadApiKey("aliyun", "ALIYUN_API_KEY");
        LoadApiKey("baidu", "BAIDU_API_KEY");
        LoadApiKey("azure", "AZURE_API_KEY");
        LoadApiKey("google", "GOOGLE_API_KEY");
    }

    private void LoadApiKey(string vendorId, string envVarName)
    {
        var value = Environment.GetEnvironmentVariable(envVarName);
        if (!string.IsNullOrWhiteSpace(value))
        {
            _apiKeys[vendorId] = value;
        }
    }

    public string GetApiKey(string vendorId)
    {
        return _apiKeys.TryGetValue(vendorId, out var key) ? key : "";
    }
}
