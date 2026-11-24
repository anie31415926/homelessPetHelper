# 小院战斗表现音效

本机 ComfyUI 0.37.1、Stable Audio Open 1.0 / t5-base，沿用[模型来源与商用检查边界](../README.md#许可边界)。生成工作流、请求回执、原始 FLAC、裁剪区间与 WAV 哈希和本目录一同保存。仅用原生节点，未调用付费云服务。

| 游戏 Cue | 导出候选 | 时长 |
| --- | --- | --- |
| `siege.toy-drop` | `toy-drop.wav` | 143 ms |
| `siege.paper-shot` | `paper-shot.wav` | 223 ms |
| `siege.paper-impact` | `paper-knock.wav` | 145 ms |
| `siege.toy-retire` | `toy-retire.wav` | 202 ms |
| `siege.kennel-ball` | `kennel-ball.wav` | 233 ms |

首个 `paper-impact` 原件低于有效信号门限，未接入；替代候选 `paper-knock` 使用新种子和提示词，不覆盖原始记录。五个导出合计 83,598 bytes，44.1 kHz 单声道 PCM16、无削波，Unity 运行 WAV 与候选按 SHA-256 核对一致。当前为 Main 随包的临时试玩素材；[顺序试听](audition.wav)不代表人耳混音、真机或正式来源许可已经验收。

生成可复用上层 `generate.mjs yard-combat-20261124`，后处理运行本目录 `process.py`，不覆盖上层基础音效批次。
