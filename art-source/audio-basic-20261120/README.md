# 基础音效试听批次

2026-11-20。用户要求使用 ComfyUI，按猫狗竖屏休闲救助游戏的温和、轻巧、可爱反馈方向制作。全部由本地 RTX 4080 生成，未调用付费云服务，未安装额外节点或修改 Python 环境。

## 当前试听稿

**只试听 v2**：[连续试听 WAV](v2/audition.wav)，顺序如下，相邻声音间隔 0.7 秒。每个文件可单独比较，文件名是候选用途，不表示已验证声音完全符合提示词。

| 用途 | 文件 | 时长 | PCM WAV 字节 |
| --- | --- | --- | --- |
| 按钮轻点 | [ui-tap](v2/candidates/ui-tap.wav) | 0.059 s | 5,278 |
| 弹窗打开 | [ui-open](v2/candidates/ui-open.wav) | 0.120 s | 10,664 |
| 合成 | [merge](v2/candidates/merge.wav) | 0.381 s | 33,650 |
| 救助成功 | [rescue-success](v2/candidates/rescue-success.wav) | 1.700 s | 149,984 |
| 奖励 | [reward](v2/candidates/reward.wav) | 0.586 s | 51,712 |
| 温和提醒 | [gentle-notice](v2/candidates/gentle-notice.wav) | 0.996 s | 87,874 |

六条合计 **339,162 bytes，约 331.2 KiB**，44.1 kHz / 单声道 / PCM16。此数是候选 WAV 源文件大小，不是 Unity 或微信最终下载量。没有导入 Assets、绑定 AudioCatalog 或新增业务播放点，游戏当前仍不会因本批次自动发声。

当前唯一试听登记：[v2/manifest.json](v2/manifest.json)。所有条目均为 `candidate`，听感、商用许可、目标设备均 `pending`，加载时机 `unassigned`。自动裁短可能截断音符/尾音，必须实际试听选用；没有冒称已听过、没有人声或已适合连续播放。

## 来源与生成

- ComfyUI **0.37.1**，Git `3f767e7f67bc587e88d6de6668eb424f725d4649`；Python 3.13.12 / PyTorch 2.10.0+cu130。未修改 ComfyUI 源码。
- 使用官方原生节点，独立启动 `127.0.0.1:8191`，禁用所有 custom/API nodes；用户目录、输入/输出/临时目录隔离于本批次的 `server/`，不共用已有会话队列。生成完成后卸载模型并停止本轮服务。
- 模型：Comfy-Org `stable-audio-open-1.0`，加 ComfyUI-Wiki `t5-base`，共 5,745,535,406 bytes。权重位于仓库外 `C:/Users/Admin/ComfyUI-Shared/models/`，下载后 SHA-256 与 Hugging Face LFS 公示值逐一匹配。精确 revision、URL、哈希及许可元数据见 [models.json](provenance/models.json)。没有把权重复制入项目。
- [官方工作流快照](provenance/official-workflow-template.json)作为原生节点参考；本轮完整 API 图为 [v2/workflows](v2/workflows/)，正负提示、种子、时长与采样设置见 [v2/plan.json](v2/plan.json)。60 steps / CFG 5 / dpmpp_3m_sde_gpu / exponential；完整 latent 47.6 秒，目标内容 3 秒，保存前 `AudioAdjustVolume -18 dB` 留出余量。
- 每条任务的提交回执与历史在 [v2/history](v2/history/)，母带在 [v2/raw](v2/raw/)。模型输出长静音保留在 FLAC 原件，不复制为运行资源。
- 后处理 [process.py](process.py)：折单声道、选择最高能量短片段、修剪边缘低电平、去直流、3 ms 淡入 / 18 ms 淡出、按用途设 -6 至 -10 dBFS 峰值，再导出 PCM16。精确裁切采样、增益和哈希写入 manifest；没有合成新声音或用程序音冒充 ComfyUI 生成。不同响度尚未通过人工听感匹配。

## 试验记录

首轮根目录 `raw/`、`candidates/`、`audition.wav` 仅保留追溯，**不建议使用**，登记已移到 [v1-experiment-manifest.json](v1-experiment-manifest.json)。首轮短 latent 下，两条原件存在削波（ui-tap、reward），另两条声音过稀疏；降最终音量无法修复已削波原件，因此改用 v2 重生成全部六条。v2 母带及导出样本均无满幅削波样本。

另保留一次 SaveAudioAdvanced 的导出错误：API 的动态格式字段必须为 `"format": "flac"`，不能直接传对象。首次 ui-tap 仅导出失败，其错误历史和原始图已保留；修正导出后复用模型节点缓存完成，不伪称首轮无失败。所有成功任务均保留 prompt ID，不因等待/下载重复提交。

## 许可边界

此批次用于试听评估，**未作正式商用许可验收**。已归档模型仓库当时的 [许可原文](provenance/stable-audio-LICENSE.md)及 [模型说明](provenance/stable-audio-README.md)。该 Stability AI Community License 文本列出商业使用登记、关联实体合计年收入超过 100 万美元需另行许可等条件；不能仅因本地免费生成就当作可无条件商用。t5 重打包仓库的来源/转换许可链也须一并复核。未代用户注册商业许可或接受企业授权。

模型、来源、参考物或提示词变化后应建立新候选，不覆盖本批次。正式接入时再按用户试听选择、音量混音和微信真机结果确定素材及加载时机。

## 检查与复现

```powershell
node art-source/audio-basic-20261120/prepare.mjs
node art-source/audio-basic-20261120/generate.mjs v2
& 'C:/Users/Admin/ComfyUI-Installs/ComfyUI/ComfyUI/.venv/Scripts/python.exe' art-source/audio-basic-20261120/process.py v2
node art-source/audio-basic-20261120/verify.mjs
```

生成前须先按上述隔离目录、模型搜索路径启动本地 ComfyUI；已有输出会跳过、已有回执只恢复查询，不重抽种子。若要重新生成，建立新批次/variant，不删除本批次回执。seed 和完整图有助追溯，不承诺不同硬件/软件逐样本一致。

实际验证：FLAC 解码、WAV 导出后重读、有限数/非静音、折单声道相消、母带/导出削波、时长及峰值、六条完整成功回执、提示词/种子对应和文件 SHA-256。结果见 [verification.json](verification.json)；同一原件重复后处理的 manifest 和 WAV 哈希一致。脚本语法检查通过。

本任务未修改图片，不扩展仅管理图片的 resource-catalog.json，未把图片检查当音效验证；未运行 Unity 测试、未做人工听感/连续叠播/微信真机验收，未提交或推送。
