#!/bin/bash

echo "========================================"
echo "VoiceServiceMcp 启动脚本 (macOS/Linux)"
echo "========================================"
echo ""

# 检查 .env 文件是否存在
if [ ! -f .env ]; then
    echo "[警告] 未找到 .env 文件"
    echo "请复制 .env.example 为 .env 并配置 API Keys"
    echo ""
    read -p "按任意键退出..."
    exit 1
fi

# 加载 .env 文件中的环境变量
export $(grep -v '^#' .env | xargs)

echo "[信息] 环境变量已加载"
echo ""

# 启动服务器
echo "[信息] 启动 MCP 服务器..."
dotnet run

read -p "按任意键退出..."
