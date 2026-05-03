# VoiceOps 文档地图

最后更新：2026-05-04

这个目录存放项目文档、厂商接入记录、阶段性清单和 Superpowers 规划稿。阅读时优先看维护中的主文档，不要把历史计划当成当前实现说明。

## 推荐阅读顺序

| 目标 | 文档 |
| --- | --- |
| 了解项目结构 | `project/PROJECT_STRUCTURE.md` |
| 接手当前进度 | `project/HANDOFF_2026-05-02.md` |
| 看功能缺口和后续优先级 | `project/TTS_TOOL_GAP_CHECKLIST_2026-05-02.md` |
| 对接火山引擎 TTS | `providers/Volcengine_TTS_Integration_Guide.md` |
| 对接阿里云 TTS | `providers/Aliyun_TTS_Integration_Guide.md` |
| 对接腾讯云 TTS | `providers/Tencent_TTS_Integration_Guide.md` |
| 查看已做的小功能说明 | `features/` |
| 查看历史实现计划 | `superpowers/specs/` 和 `superpowers/plans/` |

## 当前维护约定

- `providers/` 是厂商接入事实记录，遇到真实接口坑要优先更新这里。
- `project/` 是项目级状态和清单，适合记录仍未完成或跨厂商的问题。
- `superpowers/` 里的 spec/plan 是历史执行记录，不保证代表最新代码。
- 根目录下旧的火山鉴权指南、项目结构和交接文档已经迁到 `Docs/project/` 或合并进 `providers/Volcengine_TTS_Integration_Guide.md`。

## 火山文档速记

火山引擎是当前文档里最容易混淆的一组：`AppID/Access Token`、`V3 API Key`、`ResourceId`、AK/SK 分属不同用途。新问题请尽量追加到 `providers/Volcengine_TTS_Integration_Guide.md` 的“已确认的坑”和“常见错误排查表”，避免散落到临时笔记。
