using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using VoiceServiceMcp.Core;

namespace VoiceServiceMcp.McpServer;

[ApiController]
[Route("mcp")]
public class McpController : ControllerBase
{
    private readonly TtsService _ttsService;
    private readonly ConfigService _configService;
    private readonly ILogger<McpController> _logger;

    public McpController(TtsService ttsService, ConfigService configService, ILogger<McpController> logger)
    {
        _ttsService = ttsService;
        _configService = configService;
        _logger = logger;
    }

    [HttpPost("sse")]
    public async Task HandleSse()
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        _logger.LogInformation("MCP SSE 连接建立");

        try
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var requestBody = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(requestBody))
            {
                await SendError(null, -32700, "请求体为空");
                return;
            }

            McpRequest? mcpRequest;
            try
            {
                mcpRequest = JsonSerializer.Deserialize<McpRequest>(requestBody);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON 解析失败");
                await SendError(null, -32700, "JSON 解析失败");
                return;
            }

            if (mcpRequest == null)
            {
                await SendError(null, -32600, "无效的请求");
                return;
            }

            _logger.LogInformation($"收到 MCP 请求: {mcpRequest.Method}");

            // 处理不同的 MCP 方法
            object? result = mcpRequest.Method switch
            {
                "initialize" => HandleInitialize(),
                "tools/list" => HandleToolsList(),
                "tools/call" => await HandleToolsCall(mcpRequest),
                _ => null
            };

            if (result != null)
            {
                await SendResult(mcpRequest.Id, result);
            }
            else
            {
                await SendError(mcpRequest.Id, -32601, $"未知方法: {mcpRequest.Method}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理 MCP 请求时发生错误");
            await SendError(null, -32603, $"内部错误: {ex.Message}");
        }
    }

    private object HandleInitialize()
    {
        return new
        {
            protocolVersion = "2024-11-05",
            capabilities = new
            {
                tools = new { }
            },
            serverInfo = new
            {
                name = "VoiceServiceMcp",
                version = "1.0.0"
            }
        };
    }

    private object HandleToolsList()
    {
        var tools = TtsTools.GetAllTools();
        return new { tools };
    }

    private async Task<object?> HandleToolsCall(McpRequest request)
    {
        try
        {
            var paramsJson = JsonSerializer.Serialize(request.Params);
            var toolCallParams = JsonSerializer.Deserialize<ToolCallParams>(paramsJson);

            if (toolCallParams == null || string.IsNullOrEmpty(toolCallParams.Name))
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = "错误: 缺少工具名称"
                        }
                    }
                };
            }

            // 获取基础 URL（用于生成音频文件的访问链接）
            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            var result = await TtsTools.ExecuteTool(
                toolCallParams.Name,
                toolCallParams.Arguments,
                _ttsService,
                _configService,
                baseUrl
            );

            var resultJson = JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = resultJson
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "工具调用失败");
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"错误: {ex.Message}"
                    }
                }
            };
        }
    }

    private async Task SendResult(object? id, object result)
    {
        var response = new McpResponse
        {
            Id = id,
            Result = result
        };

        var json = JsonSerializer.Serialize(response);
        await Response.WriteAsync($"data: {json}\n\n");
        await Response.Body.FlushAsync();
    }

    private async Task SendError(object? id, int code, string message)
    {
        var response = new McpResponse
        {
            Id = id,
            Error = new McpError
            {
                Code = code,
                Message = message
            }
        };

        var json = JsonSerializer.Serialize(response);
        await Response.WriteAsync($"data: {json}\n\n");
        await Response.Body.FlushAsync();
    }
}
