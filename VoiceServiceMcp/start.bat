@echo off
echo ========================================
echo VoiceServiceMcp 启动脚本
echo ========================================
echo.

REM 检查 .env 文件是否存在
if not exist .env (
    echo [警告] 未找到 .env 文件
    echo 请复制 .env.example 为 .env 并配置 API Keys
    echo.
    pause
    exit /b 1
)

REM 加载 .env 文件中的环境变量
for /f "usebackq tokens=1,2 delims==" %%a in (".env") do (
    set "%%a=%%b"
)

echo [信息] 环境变量已加载
echo.

REM 启动服务器
echo [信息] 启动 MCP 服务器...
dotnet run

pause
