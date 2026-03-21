# 音色 ID 复制功能说明

## 功能概述
在每个音色卡片的播放按钮旁边添加了一个复制按钮，可以快速复制音色 ID 到剪贴板。

## 修改内容

### 1. UI 修改 (Components/Pages/Workspace.razor)
- 在播放按钮后添加了复制按钮
- 复制按钮使用复制图标（默认状态）和对勾图标（成功状态）
- 点击按钮时阻止事件冒泡，避免触发卡片选择

### 2. 功能实现 (Components/Pages/Workspace.razor)
添加了以下功能：
- `CopyVoiceId(string voiceId)` 方法：使用浏览器 Clipboard API 复制音色 ID
- `_copiedVoiceId` 字段：跟踪当前已复制的音色 ID
- `_copyResetCts` 字段：用于取消之前的重置任务
- 复制成功后显示 2 秒的成功状态，然后自动恢复

### 3. 样式添加 (wwwroot/css/app.css)
添加了 `.voice-copy-btn` 样式类：
- 默认状态：灰色背景，与播放按钮风格一致
- 悬停状态：边框和文字颜色变亮
- 成功状态 (`.copied`)：绿色背景和边框，显示对勾图标

## 使用方法
1. 在音色浏览器中找到想要的音色
2. 点击音色卡片右上角的复制按钮（📋 图标）
3. 按钮会变成绿色并显示对勾 ✓，表示复制成功
4. 2 秒后按钮自动恢复到默认状态
5. 音色 ID 已复制到剪贴板，可以直接粘贴使用

## 状态提示
- **默认状态**：灰色圆形按钮，显示复制图标
- **成功状态**：绿色圆形按钮，显示对勾图标，持续 2 秒
- **失败处理**：如果复制失败（如浏览器不支持 Clipboard API），按钮会立即恢复默认状态

## 技术细节
- 使用 `navigator.clipboard.writeText()` API 进行复制
- 使用 `CancellationTokenSource` 管理状态重置任务
- 使用 `@onclick:stopPropagation="true"` 防止事件冒泡
- 复制操作完全在客户端完成，无需服务器交互

## 兼容性
- 需要 HTTPS 或 localhost 环境才能使用 Clipboard API
- 支持所有现代浏览器（Chrome、Edge、Firefox、Safari）
