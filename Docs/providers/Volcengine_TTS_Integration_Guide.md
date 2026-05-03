# 火山引擎 Volcengine TTS 接入指南

最后更新：2026-05-04

本文是 VoiceOps 项目中火山引擎 TTS 的主文档。火山相关说明统一放在这里维护；根目录下旧的火山鉴权指南和长篇研究稿已经合并进本文，不再单独保留。

## 1. 先分清四类凭证

火山引擎最容易混淆的地方不是接口本身，而是不同控制台页面生成的“密钥”服务于不同产品线。

| 凭证 | 控制台位置 | VoiceOps 用途 | 是否必填 |
| --- | --- | --- | --- |
| `AppID` + `Access Token` | 语音合成 / 豆包语音应用配置 | 生成语音；旧版 `/api/v1/tts` 和部分 V3 Header 鉴权都可用 | 基础生成推荐填 |
| `V3 API Key` | 豆包语音 / TTS API Key 页面 | V3 数据面生成语音，请求头为 `X-Api-Key` | 可选，配置后可不依赖 AppID/Token 走 V3 |
| `ResourceId` | V3 TTS 文档或控制台资源说明 | 指定模型资源，例如 `seed-tts-2.0`、`seed-tts-1.0`、`seed-icl-2.0` | 可选；默认按音色/模型推断 |
| `Access Key` + `Secret Key` | IAM API 访问密钥页面 | 控制面 OpenAPI：刷新音色库、拉情感示例、管理 API Key | 仅“刷新在线音色库”需要 |

方舟 Ark 的模型推理 API Key 主要用于 LLM/对话模型，不等同于 TTS 数据面的 `V3 API Key`。除非你确认页面标题和文档都指向“豆包语音 API Key”，不要把 Ark 聊天 API Key 填进火山 TTS 的 `V3 API Key` 字段。

常用控制台入口：

| 链接 | 页面含义 | 在本项目里的用途 |
| --- | --- | --- |
| [IAM API 访问密钥](https://console.volcengine.com/iam/keymanage/) | 管理火山账号级 `Access Key` / `Secret Key` | 填到火山高级项的 `Access Key`、`Secret Key`，用于控制面 OpenAPI，例如测试连通、刷新 `ListSpeakers` 音色库 |
| [方舟 API Key 管理](https://console.volcengine.com/ark/region:ark+cn-beijing/apiKey?apikey=%7B%7D&projectName=default) | 管理 Ark 大模型推理 API Key | 通常不用于本项目的火山 TTS 生成；主要用于 LLM/聊天/文本生成接口，避免误填到 TTS 的 `V3 API Key` |
| [豆包语音 / TTS 语音合成](https://console.volcengine.com/ark/region:ark+cn-beijing/tts/speechSynthesis) | 管理豆包语音合成能力、音色、TTS 相关 Key 或资源 | 获取/核对 TTS 生成需要的 `AppID`、`Access Token`、TTS `V3 API Key`、`ResourceId`、音色授权和合成资源 |

VoiceOps 设置页的火山存储顺序为：

```text
AppID | AccessToken | Cluster | AccessKey | SecretKey | V3 API Key | ResourceId
```

## 2. 当前实现边界

| 能力 | 桌面端现状 | MCP 现状 | 备注 |
| --- | --- | --- | --- |
| 基础短文本生成 | 已接入 V3，必要时回退旧版 `/api/v1/tts` | 已接入 V3/旧版路径 | V3 API Key 失败不回退；AppID/Token 失败才尝试旧版 |
| 长文本生成 | 文本超过 3000 字符走 V3 submit/query | 有 V3 submit/query 路径 | 项目阈值是 `HuoshanTtsProtocol.AsyncTextThreshold = 3000` |
| 在线音色库刷新 | `ListSpeakers` + `ListBigModelTTSTimbres` 合并 | 有独立实现 | 需要 AK/SK；桌面端会缓存到 `%APPDATA%/VoiceOps/Cache/voices_huoshan.json` |
| 音色 ResourceId | 桌面端优先使用在线音色返回的 `resource_id` | 主要靠请求 model/config 推断 | 桌面端选择音色时会同步模型下拉到匹配资源 |
| emotion 语气 | 桌面端按音色元数据展示；离线内置音色保留 fallback | MCP 请求模型暂未暴露 emotion 入参 | `general` / `neutral` 不作为实际 emotion 发送 |
| emotion_scale | 未接入 | 未接入 | 官方支持 1-5，但 UI 和请求模型还没有字段 |
| SSML / timestamp / mix speaker | 未产品化 | 未产品化 | 官方 V3 支持部分能力，项目尚未抽象到 UI |
| 输出格式/采样率 | 固定 mp3 / 24000 Hz | 固定 mp3 / 24000 Hz | 目前 UI 没有格式和采样率选择 |

这张表描述的是当前代码状态，不代表火山官方能力上限。官方文档会继续变化，新增字段前要先确认数据面接口和音色授权是否匹配。

## 3. 当前项目的调用链

### 3.1 测试连通

如果只填了 `AppID` + `Access Token` 或 `V3 API Key`，测试连通只做格式检查，不发起合成，避免消耗额度。

如果同时填了 AK/SK，测试连通会调用控制面 OpenAPI：

```text
POST https://open.volcengineapi.com/?Action=ListSpeakers&Version=2025-05-20
Service: speech_saas_prod
Region: cn-beijing
Body: {"Limit":1,"Page":1}
```

这个请求只验证“控制面能否访问音色列表”，不代表语音生成一定能成功。生成还要看 `AppID/Access Token` 或 `V3 API Key`、`ResourceId`、音色授权和额度。

### 3.2 刷新在线音色库

刷新音色库需要 AK/SK。桌面端会先分页调用 `ListSpeakers` 拉当前账号可用音色，再调用 `ListBigModelTTSTimbres` 补充大模型情绪示例：

```text
ListSpeakers             -> 基础音色信息、resource_id、trial_url
ListBigModelTTSTimbres   -> 情绪/风格示例、demo_url、emotion_type
```

`ListSpeakers` 更适合作为“当前账号能用哪些音色”的权威来源。不要靠旧文档里的静态音色表手填。

桌面端刷新成功后会把完整音色列表缓存到：

```text
%APPDATA%/VoiceOps/Cache/voices_huoshan.json
```

下次进入火山工作区会优先加载缓存。若控制台刚调整了音色授权或资源版本，先点“刷新音色库”，不要只看旧缓存。

当前 provider 还会把原始调试响应写到用户设置的输出目录：

```text
huoshan_speakers_page_*.json
huoshan_timbres_debug.json
```

这些文件只用于排查 OpenAPI 返回结构，不是最终音频产物。后续应迁到 `.local/logs` 或 `%APPDATA%/VoiceOps/Diagnostics`，避免污染用户输出目录。

### 3.3 生成短文本语音

短文本优先走 V3：

```text
POST https://openspeech.bytedance.com/api/v3/tts/unidirectional
```

V3 Header 有两种模式：

```text
X-Api-Key: <V3 API Key>
X-Api-Resource-Id: <ResourceId>
X-Api-Request-Id: <uuid>
```

或：

```text
X-Api-App-Id: <AppID>
X-Api-Access-Key: <Access Token>
X-Api-Resource-Id: <ResourceId>
X-Api-Request-Id: <uuid>
```

如果使用 `AppID + Access Token` 调 V3 失败，项目会尝试回退旧版 `/api/v1/tts`。如果使用 `V3 API Key` 调 V3 失败，则直接返回 V3 错误，不再回退。

V3 单向流式接口不是一次性返回完整 JSON 音频，而是按行返回事件。当前解析规则是：

```text
data: {"code":0,"data":"<base64 chunk>"}      -> 音频片段
{"code":20000000,"message":"ok"}              -> 结束
data: {"code":45000000,"message":"..."}       -> 错误
```

如果最后没有收集到任何音频片段，项目会报“火山 V3 未返回音频数据”。这种情况优先检查响应行格式、音色权限、ResourceId 和错误码，不要只看 HTTP 2xx。

V3 的语气参数要放在 `req_params.audio_params.emotion`，不要放在 `req_params.emotion`。正确结构类似：

```json
{
  "user": {
    "uid": "voice_ops"
  },
  "req_params": {
    "text": "今天真开心。",
    "speaker": "zh_female_tianxinxiaomei_emo_v2_mars_bigtts",
    "audio_params": {
      "format": "mp3",
      "sample_rate": 24000,
      "speech_rate": 0,
      "loudness_rate": 0,
      "emotion": "sad"
    }
  }
}
```

如果未选择语气，或选择的是 `general` / `neutral` 这类默认语气，项目应省略 `emotion` 字段，而不是把默认值硬传给接口。

### 3.4 生成长文本语音

超过项目阈值的长文本走 V3 异步：

```text
POST https://openspeech.bytedance.com/api/v3/tts/submit
POST https://openspeech.bytedance.com/api/v3/tts/query
```

长文本更依赖 `ResourceId` 与音色授权匹配。若返回 `requested resource not granted`、`access denied` 或 speaker permission 相关错误，优先检查资源模型版本和音色是否已开通。

当前轮询策略最多查询 30 次：第一次等待 1 秒，之后每次等待 2 秒。若服务端还没完成，项目会返回 task_id 并提示稍后查询或缩短文本。火山官方长文本结果 URL 有有效期，拿到 `audio_url` 后要尽快下载并保存。

## 4. 已确认的坑

### 坑 1：OpenAPI 失败不能笼统说 AK/SK 错

早期实现把非 2xx 响应直接吞掉，界面只显示“请检查 AK/SK 和语音服务权限”。这会误导排查，因为真实原因可能是：

- `InvalidParameter`：请求体参数不符合接口实际要求。
- `SignatureDoesNotMatch`：签名文本、Header 或 Body hash 不一致。
- `AccessDenied`：AK/SK 有效，但没有语音服务控制面权限。
- `InvalidActionOrVersion`：Action 或 Version 不存在。

现在项目会把 HTTP 状态码和火山返回的错误码透出。排查时先看错误码，不要直接重置密钥。

### 坑 2：`ListSpeakers` 的 `Limit` 必须是数字

这是本次实测确认的新坑。`ListSpeakers` 接口接受：

```json
{"Limit":1,"Page":1}
```

它会拒绝：

```json
{"Limit":"1","Page":1}
```

也会拒绝小写字段：

```json
{"limit":"1","page":1}
```

错误通常是：

```text
BadRequest: InvalidParameter - Invalid parameter
```

注意：部分 SDK/文档片段会让人以为 `limit` 是字符串或小写字段，但 `open.volcengineapi.com` 当前实测要求是 PascalCase 字段，并且 `Limit` 为数字。空 `{}` 也能返回成功，但不适合分页拉全量。

### 坑 3：OpenAPI 签名对 Content-Type 极敏感

AK/SK 调 OpenAPI 走 HMAC-SHA256 签名。签名文本里的 `content-type` 必须和实际请求头一致。项目当前固定为：

```text
application/json; charset=UTF-8
```

如果签名时是 `UTF-8`，实际发送变成 `utf-8`，就可能出现 `SignatureDoesNotMatch`。修改签名器时要同时看 CanonicalRequest 和最终 HttpClient 发出的 Header。

### 坑 4：.NET 里 Authorization 要绕过格式校验

火山 OpenAPI 的 Authorization 值里包含：

```text
SignedHeaders=content-type;host;x-content-sha256;x-date
```

分号对 .NET 标准 Header 解析不友好。项目里必须使用：

```csharp
request.Headers.TryAddWithoutValidation("Authorization", authHeader);
```

不要改成 `AuthenticationHeaderValue` 或普通强校验添加方式，否则请求甚至可能发不出去。

### 坑 5：旧版 TTS 的 Bearer 中间是分号

旧版 `/api/v1/tts` 请求头不是标准 OAuth 写法：

```text
Authorization: Bearer;<Access Token>
```

不是：

```text
Authorization: Bearer <Access Token>
```

旧版请求体里的 `app.token` 字段也不是你的真实 Token，而是字面量：

```json
{
  "app": {
    "appid": "你的 AppID",
    "token": "access_token",
    "cluster": "volcano_tts"
  }
}
```

真实 Token 只放在 Header 里。

### 坑 6：Cluster 只属于旧版/标准音色语境

标准音色通常使用：

```text
cluster = volcano_tts
```

大模型音色常见特征包括 `_bigtts`、`_tob`、`_moon_`、`_mars_`、`_wvae_`、`ICL_`、`multi_`。项目遇到这些音色时会把旧版请求里的 `cluster` 置空，避免把大模型音色误发到标准集群。

V3 路径更看重 `X-Api-Resource-Id`，不要把 `Cluster` 和 `ResourceId` 混为一谈。

### 坑 7：ResourceId 不是密钥

`ResourceId` 用来选择资源/模型版本。项目默认推断规则：

| 条件 | ResourceId |
| --- | --- |
| 显式填写 ResourceId | 使用填写值 |
| modelId 以 `seed-` 开头 | 使用 modelId |
| `saturn_` 或 `ICL_` 音色 | `seed-icl-2.0` |
| `BV` / `VC_BV` 或 1.0 模型 | `seed-tts-1.0` |
| 其他 | `seed-tts-2.0` |

如果资源和音色不匹配，常见错误是资源未授权或 speaker permission denied。

### 坑 8：AK/SK 只能证明控制面，不能证明发声权限

AK/SK 成功调用 `ListSpeakers` 只说明“这个账号能访问语音 SaaS 控制面”。真正生成语音还可能因为下列原因失败：

- 没填或填错 `AppID/Access Token`。
- `V3 API Key` 来自 Ark 模型推理页面，不是 TTS 数据面。
- 音色没有下单或没有授权。
- `ResourceId` 与音色版本不匹配。
- 额度、并发或项目权限不足。

### 坑 9：V3 `emotion` 放错层级会让语气“选了但不生效”

本次实测遇到的现象：界面上 `甜心小美` 能显示 `悲伤`、`恐惧`、`厌恶`、`中性`，生成也成功，但听感像没有应用语气。排查后确认这些选项不是 UI 瞎猜：官方音色列表和在线缓存都声明该音色支持这些 emotion。真正的问题是请求体把 `emotion` 放在了错误位置。

错误写法：

```json
{
  "req_params": {
    "speaker": "zh_female_tianxinxiaomei_emo_v2_mars_bigtts",
    "audio_params": {
      "format": "mp3",
      "sample_rate": 24000
    },
    "emotion": "sad"
  }
}
```

正确写法：

```json
{
  "req_params": {
    "speaker": "zh_female_tianxinxiaomei_emo_v2_mars_bigtts",
    "audio_params": {
      "format": "mp3",
      "sample_rate": 24000,
      "emotion": "sad"
    }
  }
}
```

同一规则也适用于 V3 异步长文本提交 `/api/v3/tts/submit`：`emotion` 仍然挂在 `req_params.audio_params` 下。旧版 `/api/v1/tts` 例外，它使用的是 `audio.emotion`。

另外要注意 `general` 和 `neutral`。`general` 常来自在线音色元数据，代表通用/默认示例；`neutral` 是中性。它们都不应该作为实际语气控制参数强行发送。项目里应先规范化：

```text
general -> ""
neutral -> ""
sad     -> "sad"
fear    -> "fear"
hate    -> "hate"
```

### 坑 10：官方支持的 V3 能力不等于项目已接入

官方 V3 文档里还能看到 `emotion_scale`、`ssml`、`enable_timestamp`、`bit_rate`、`additions`、`mix_speaker` 等字段。当前 VoiceOps 没有把这些字段做成 UI 控件，也没有加入 `TtsRequest` 的统一能力模型。排查时不要因为官方支持就默认项目已经传了这些参数。

尤其是 `emotion_scale`：它需要在设置了 `emotion` 后放到 `req_params.audio_params.emotion_scale`，范围 1-5，默认值通常为 4。现在项目只传 `emotion`，不传强度；如果用户觉得情绪不够明显，下一步应该新增字段和 UI，而不是把强度塞到文本或 emotion 名称里。

### 坑 11：桌面端和 MCP 的火山能力会分叉

桌面端现在已经会从选中音色的分类里取 `resource_id` 并传给生成请求，MCP 侧的请求模型目前只有 `VendorId`、`ModelId`、`VoiceId`、`Text`、`Speed`。这意味着：

- 桌面端选在线音色时，`seed-tts-1.0` / `seed-tts-2.0` 更容易自动匹配。
- MCP 如果只给 voice id，可能仍依赖默认推断或手动配置的 ResourceId。
- MCP 暂时没有 emotion 入参，即使音色库返回了 `Emotions`，生成工具也不能直接选择语气。

后续如果要让 MCP 追平桌面端，应优先把 `Emotion`、`ResourceId`、`Volume`、`InputFormat` 等字段下沉到共享请求模型或 MCP 参数 schema。

## 5. 常见错误排查表

| 错误或现象 | 优先判断 | 处理建议 |
| --- | --- | --- |
| `InvalidParameter` on `ListSpeakers` | 请求体字段或类型不对 | 确认发送 `{"Limit":1,"Page":1}`，不是字符串 Limit 或小写 limit |
| `SignatureDoesNotMatch` | OpenAPI 签名不一致 | 检查 Content-Type、Query 排序、Body hash、X-Date、AK/SK 是否含空格 |
| `AccessDenied` | AK/SK 有效但权限不足 | 给子账号补语音技术权限，确认服务已开通 |
| `InvalidActionOrVersion` | Action/Version 错 | `ListSpeakers` 当前使用 `Version=2025-05-20` |
| `invalid authorization` / `unauthorized` | 数据面鉴权失败 | 检查 `AppID/Access Token` 或 `V3 API Key` |
| `requested resource not granted` | 资源未授权 | 检查 `ResourceId` 与音色模型版本、购买/授权状态 |
| `speaker permission denied` | 音色不可用 | 用 `ListSpeakers` 刷新当前账号可用音色，避免手填 |
| UI 能显示语气选项但听感不变 | V3 `emotion` 可能放错层级，或发送了 `general` / `neutral` | 确认 JSON 是 `req_params.audio_params.emotion`；默认语气应省略 `emotion` |
| `火山 V3 未返回音频数据` | V3 流式响应没有解析到 base64 音频片段 | 打印响应行，确认是否是 `data:` SSE、终止码或隐藏错误 |
| 长文本返回 task_id 但没有音频 | 异步任务仍在 Running，或轮询时间不够 | 记录 task_id，稍后 query；必要时缩短文本或调长轮询策略 |
| 选择 1.0 多情感音色但 V3 报资源/权限错误 | ResourceId 使用了 `seed-tts-2.0` 或账号未授权该音色 | 刷新音色库，优先使用音色返回的 `resource_id`；必要时手动填 `seed-tts-1.0` |
| 调了 `emotion_scale` 但没效果 | 项目当前没有传该字段，或字段层级错误 | 应放在 `req_params.audio_params.emotion_scale`，并先扩展请求模型和 UI |
| `quota exceeded` / 并发 | 配额不足 | 降并发、检查套餐或增购 |
| `3050` / 音色不存在 | voice/speaker ID 错 | 以 `ListSpeakers` 返回的 `voice_type` 为准 |

## 6. 推荐调试顺序

1. 只填 `AppID + Access Token`，先确认基础生成路径是否可用。
2. 再填 AK/SK，点击测试连通，确认 `ListSpeakers` 能返回 2xx。
3. 刷新在线音色库，优先选择 `ListSpeakers` 返回的音色，不手抄旧表。
4. 如果要走 V3，确认 `V3 API Key` 是豆包语音/TTS API Key，不是 Ark LLM Key。
5. 检查 `ResourceId` 是否和选中音色匹配。
6. 如果是语气问题，先确认音色元数据里确实有目标 emotion，再抓请求体看 `req_params.audio_params`。
7. 出错时记录 `X-Api-Request-Id`、HTTP 状态码、火山错误码，再按上表排查。
8. 如果涉及 MCP，确认 MCP 请求 schema 是否真的暴露了桌面端已有的参数。

## 7. 参考入口

- 语音控制台：https://console.volcengine.com/speech/
- IAM API 访问密钥：https://console.volcengine.com/iam/keymanage
- API Explorer：https://console.volcengine.com/api-explorer
- 火山统一 API 签名规范：https://www.volcengine.com/docs/6257/67215
- 语音合成 API 参数说明：https://www.volcengine.com/docs/6561/79823
- 大模型语音合成音色列表：https://www.volcengine.com/docs/6561/1257544
- V3 API Key 使用说明：https://www.volcengine.com/docs/6561/1816214
- V3 ResourceId / 长文本 / emotion_scale 说明：https://www.volcengine.com/docs/6561/1829010
