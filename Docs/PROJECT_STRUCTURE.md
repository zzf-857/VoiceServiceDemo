# VoiceServiceDemo 项目地图

这份文件用来帮助新手快速判断“东西应该放在哪里”。以后新增功能时，先看这里，尽量不要把临时文件和业务代码混在根目录。

## 根目录

- `VoiceServiceDemo.slnx`：整个项目的解决方案入口，包含桌面端、MCP、共享库和自检项目。
- `VoiceServiceDemo.csproj`：桌面端 WPF + Blazor Hybrid 应用。
- `README.md` / `README_en.md`：项目使用说明。
- `App.xaml`、`MainWindow.xaml`：桌面应用启动和窗口文件。

## 业务代码

- `Components/`：Blazor 页面和布局。页面过大时，优先拆成子组件。
- `Services/`：桌面端业务服务。`TtsService.cs` 是统一入口，供应商细节优先放到 `Services/Providers/`。
- `Services/Providers/`：桌面端供应商实现，例如火山 TTS 的联网、音色拉取、合成流程。
- `Models/`：桌面端数据模型。
- `Helpers/`：桌面端辅助类。
- `VoiceServiceShared/`：桌面端和 MCP 端都要用的共享逻辑。供应商协议、凭证解析、请求构造应优先放这里。
- `VoiceServiceMcp/`：MCP 服务端。
- `VoiceServiceDemo.Tests/`：现有自检项目。它现在更像 smoke test，不是标准 xUnit/NUnit 测试。

## 资料和工具

- `Docs/`：项目文档、供应商接入资料、整理计划。
- `Docs/features/`：已经完成或正在整理的功能说明。
- `data/`：供应商原始数据或解析后的静态数据。
- `scripts/`：一次性或辅助脚本。
- `assets/`：设计稿、图标、非代码资源。
- `.local/`：只在本机保留的日志、临时音频、诊断文件，不进入 Git。

## 当前整理优先级

1. 保持 `bin/`、`obj/`、日志、临时音频不进入 Git。
2. 新增供应商能力时，先判断能否放进 `VoiceServiceShared/`，避免桌面端和 MCP 端重复写一遍。
3. 拆 `Services/TtsService.cs` 时按供应商拆，不要一次重构全部。火山 TTS 已经先拆到 `Services/Providers/HuoshanTtsProvider.cs`。
4. 拆 `Components/Pages/Workspace.razor` 时按界面区块拆，不要和 TTS 协议逻辑混在一起。
5. 后续把 `VoiceServiceDemo.Tests` 改成标准测试项目，或者明确改名为 smoke test。
