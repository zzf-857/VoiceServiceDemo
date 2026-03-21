using System.Text.Json;
using VoiceServiceMcp.Core;

namespace VoiceServiceMcp.McpServer;

/// <summary>
/// TTS MCP 工具定义和执行
/// </summary>
public static class TtsTools
{
    /// <summary>
    /// 获取所有工具定义
    /// </summary>
    public static List<ToolDefinition> GetAllTools()
    {
        return new List<ToolDefinition>
        {
            new ToolDefinition
            {
                Name = "generate_tts",
                Description = "生成语音音频文件。支持多个厂商：火山引擎(huoshan)、OpenAI(openai)、阿里云(aliyun)、百度(baidu)、Azure(azure)、Google(google)。",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        vendor = new
                        {
                            type = "string",
                            description = "厂商 ID",
                            @enum = new[] { "huoshan", "openai", "aliyun", "baidu", "azure", "google" }
                        },
                        voice_id = new
                        {
                            type = "string",
                            description = "音色 ID（使用 list_voices 工具获取可用音色）"
                        },
                        text = new
                        {
                            type = "string",
                            description = "要转换为语音的文本内容"
                        },
                        model_id = new
                        {
                            type = "string",
                            description = "模型 ID（可选，不同厂商有不同的模型）"
                        },
                        speed = new
                        {
                            type = "number",
                            description = "语速（可选，默认 1.0，范围通常 0.5-2.0）",
                            @default = 1.0
                        }
                    },
                    required = new[] { "vendor", "voice_id", "text" }
                }
            },
            new ToolDefinition
            {
                Name = "list_voices",
                Description = "获取指定厂商的可用音色列表。火山引擎支持在线获取完整音色库（需要配置 AK/SK）。",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        vendor = new
                        {
                            type = "string",
                            description = "厂商 ID",
                            @enum = new[] { "huoshan", "openai", "aliyun", "baidu", "azure", "google" }
                        }
                    },
                    required = new[] { "vendor" }
                }
            },
            new ToolDefinition
            {
                Name = "test_connection",
                Description = "测试指定厂商的 API 连接状态，验证 API Key 是否有效。",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        vendor = new
                        {
                            type = "string",
                            description = "厂商 ID",
                            @enum = new[] { "huoshan", "openai", "aliyun", "baidu", "azure", "google" }
                        }
                    },
                    required = new[] { "vendor" }
                }
            }
        };
    }

    /// <summary>
    /// 执行工具调用
    /// </summary>
    public static async Task<object> ExecuteTool(string toolName, Dictionary<string, object>? args, TtsService ttsService, ConfigService configService, string baseUrl)
    {
        if (args == null)
        {
            return new { error = "缺少参数" };
        }

        try
        {
            return toolName switch
            {
                "generate_tts" => await ExecuteGenerateTts(args, ttsService, configService, baseUrl),
                "list_voices" => await ExecuteListVoices(args, ttsService, configService),
                "test_connection" => await ExecuteTestConnection(args, ttsService, configService),
                _ => new { error = $"未知工具: {toolName}" }
            };
        }
        catch (Exception ex)
        {
            return new { error = $"工具执行失败: {ex.Message}" };
        }
    }

    private static async Task<object> ExecuteGenerateTts(Dictionary<string, object> args, TtsService ttsService, ConfigService configService, string baseUrl)
    {
        var vendor = GetStringArg(args, "vendor");
        var voiceId = GetStringArg(args, "voice_id");
        var text = GetStringArg(args, "text");
        var modelId = GetStringArg(args, "model_id") ?? "";
        var speed = GetDoubleArg(args, "speed") ?? 1.0;

        if (string.IsNullOrEmpty(vendor) || string.IsNullOrEmpty(voiceId) || string.IsNullOrEmpty(text))
        {
            return new { success = false, error_message = "缺少必需参数: vendor, voice_id, text" };
        }

        var apiKey = configService.GetApiKey(vendor);
        if (string.IsNullOrEmpty(apiKey))
        {
            return new { success = false, error_message = $"未配置 {vendor} 的 API Key" };
        }

        // 如果没有指定 model_id，使用默认模型
        if (string.IsNullOrEmpty(modelId))
        {
            var vendorConfig = VendorRegistry.GetById(vendor);
            modelId = vendorConfig?.DefaultModels.FirstOrDefault()?.Id ?? "";
        }

        var request = new TtsRequest
        {
            VendorId = vendor,
            ModelId = modelId,
            VoiceId = voiceId,
            Text = text,
            Speed = speed
        };

        var result = await ttsService.GenerateAsync(request, apiKey);

        if (result.Success && !string.IsNullOrEmpty(result.FilePath))
        {
            // 生成可访问的 HTTP URL
            var fileName = Path.GetFileName(result.FilePath);
            var fileUrl = $"{baseUrl}/audio/{fileName}";

            return new
            {
                success = true,
                file_path = result.FilePath,
                file_url = fileUrl,
                vendor_name = result.VendorName,
                voice_name = result.VoiceName,
                model_name = result.ModelName,
                text = result.Text,
                generated_at = result.GeneratedAt
            };
        }

        return new
        {
            success = false,
            error_message = result.ErrorMessage
        };
    }

    private static async Task<object> ExecuteListVoices(Dictionary<string, object> args, TtsService ttsService, ConfigService configService)
    {
        var vendor = GetStringArg(args, "vendor");
        if (string.IsNullOrEmpty(vendor))
        {
            return new { error = "缺少参数: vendor" };
        }

        var vendorConfig = VendorRegistry.GetById(vendor);
        if (vendorConfig == null)
        {
            return new { error = $"未知厂商: {vendor}" };
        }

        List<VoiceOption> voices;

        // 如果支持在线获取，尝试获取
        if (vendorConfig.SupportsVoiceFetch)
        {
            var apiKey = configService.GetApiKey(vendor);
            if (!string.IsNullOrEmpty(apiKey))
            {
                voices = await ttsService.FetchVoicesAsync(vendor, apiKey);
                if (voices.Any())
                {
                    return new
                    {
                        vendor = vendor,
                        vendor_name = vendorConfig.Name,
                        source = "online",
                        count = voices.Count,
                        voices = voices.Select(v => new
                        {
                            id = v.Id,
                            name = v.Name,
                            gender = v.Gender,
                            language = v.Language,
                            age = v.Age,
                            categories = v.Categories,
                            is_big_tts = v.IsBigTTS,
                            sample_url = v.SampleUrl
                        })
                    };
                }
            }
        }

        // 返回默认音色列表
        voices = vendorConfig.DefaultVoices;
        return new
        {
            vendor = vendor,
            vendor_name = vendorConfig.Name,
            source = "default",
            count = voices.Count,
            voices = voices.Select(v => new
            {
                id = v.Id,
                name = v.Name,
                gender = v.Gender,
                language = v.Language,
                is_big_tts = v.IsBigTTS
            })
        };
    }

    private static async Task<object> ExecuteTestConnection(Dictionary<string, object> args, TtsService ttsService, ConfigService configService)
    {
        var vendor = GetStringArg(args, "vendor");
        if (string.IsNullOrEmpty(vendor))
        {
            return new { success = false, message = "缺少参数: vendor" };
        }

        var apiKey = configService.GetApiKey(vendor);
        if (string.IsNullOrEmpty(apiKey))
        {
            return new { success = false, message = $"未配置 {vendor} 的 API Key" };
        }

        var (success, message) = await ttsService.TestConnectivityAsync(vendor, apiKey);
        return new { success, message };
    }

    // 辅助方法：从参数字典中获取字符串
    private static string? GetStringArg(Dictionary<string, object> args, string key)
    {
        if (args.TryGetValue(key, out var value))
        {
            if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.String)
            {
                return jsonElement.GetString();
            }
            return value?.ToString();
        }
        return null;
    }

    // 辅助方法：从参数字典中获取数字
    private static double? GetDoubleArg(Dictionary<string, object> args, string key)
    {
        if (args.TryGetValue(key, out var value))
        {
            if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Number)
            {
                return jsonElement.GetDouble();
            }
            if (double.TryParse(value?.ToString(), out var result))
            {
                return result;
            }
        }
        return null;
    }
}
