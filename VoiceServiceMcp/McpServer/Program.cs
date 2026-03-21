using VoiceServiceMcp.Core;
using VoiceServiceMcp.McpServer;

var builder = WebApplication.CreateBuilder(args);

// 从环境变量读取配置
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
var outputDir = Environment.GetEnvironmentVariable("OUTPUT_DIR") ?? "./output";

// 注册服务
builder.Services.AddSingleton(new TtsService(outputDir));
builder.Services.AddSingleton<ConfigService>();
builder.Services.AddControllers();

// 配置 CORS（允许跨域）
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 配置监听地址
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

app.UseCors();
app.UseRouting();
app.MapControllers();

// 静态文件服务（用于访问生成的音频文件）
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(Directory.GetCurrentDirectory(), outputDir.TrimStart('.', '/'))),
    RequestPath = "/audio"
});

// 健康检查端点
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

Console.WriteLine($"🚀 VoiceServiceMcp 服务器启动成功！");
Console.WriteLine($"📡 监听地址: http://0.0.0.0:{port}");
Console.WriteLine($"🎵 音频输出目录: {Path.GetFullPath(outputDir)}");
Console.WriteLine($"🔧 MCP SSE 端点: http://0.0.0.0:{port}/mcp/sse");
Console.WriteLine();
Console.WriteLine("配置的 API Keys:");
var configService = app.Services.GetRequiredService<ConfigService>();
foreach (var vendor in VendorRegistry.All)
{
    var hasKey = !string.IsNullOrEmpty(configService.GetApiKey(vendor.Id));
    Console.WriteLine($"  {vendor.Name}: {(hasKey ? "✓ 已配置" : "✗ 未配置")}");
}
Console.WriteLine();

app.Run();
