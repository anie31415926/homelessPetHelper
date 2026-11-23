# ComfyUI 临时占位 BGM

2026-11-23。**不是最终音乐**。用户随后要求先接入并提交，现已从WAV母带接入Unity临时音乐目录；不代表最终曲风、乐句循环、商用来源或目标设备已获批准。

当前运行接入唯一登记：[integration.json](integration.json)。三条都为temporary/非最终/first-play静态依赖，Main按站内、地图、清路与救助阶段选择；共用原一音乐/八音效播放器和音量/静音/暂停。制作manifest保留当时未接入的冻结事实，不覆盖生成历史或哈希。

接入验证：[unity-verification.json](unity-verification.json)。实际BGM-only提交快照22项Unity blob精确匹配；Edit832/832、Play110通过/1旧动作基线缺失跳过，补基线后该项1/1通过。原XML和统一门禁失败边界保留，完整解释见[项目记录](../../docs/占位BGM制作与规格.md#unity接入与实际提交内容)。这不是微信/听感/最终音乐验收。

Loading会话随后独立提交后，最终父内容追加验证Edit839/839、Loading+音乐Play19/19；旧111项保留为不含该Loading父内容的基线，不混称最终父内容全量Play。

## 试听与文件

下表方向是制作意图，不冒称已人工听过或完全符合提示词。全长 MP3 供便捷试听；OGG 为压缩体积/音质候选；无损 WAV 与原始 FLAC 不属于运行交付体积。

| 用途与方向 | 全长试听 | 压缩候选 | 秒 | OGG 字节 | LUFS | dBTP |
| --- | --- | --- | --- | --- | --- | --- |
| 救助站：温暖、安定，木吉他/柔和钢琴方向 | [MP3](audition/BGM_Station_Placeholder.mp3) | [OGG](candidates/BGM_Station_Placeholder.ogg) | 32 | 236926 | -20.00 | -8.64 |
| 地图探索：轻巧、好奇，拨弦/木琴方向 | [MP3](audition/BGM_Explore_Placeholder.mp3) | [OGG](candidates/BGM_Explore_Placeholder.ogg) | 32 | 252704 | -20.01 | -5.41 |
| 救助关卡：专注、有节奏，不做紧张战斗方向 | [MP3](audition/BGM_Rescue_Placeholder.mp3) | [OGG](candidates/BGM_Rescue_Placeholder.ogg) | 32 | 246902 | -19.97 | -5.96 |

OGG 合计 **736532 bytes / 719.3 KiB**。32kHz、立体声、Vorbis 目标80kbps；VBR实测字节低于按目标码率估算，不靠截断文件凑预算。所有条目在唯一音频登记 [manifest.json](manifest.json) 中为 `temporary` / `final:false` / `loadPhase:unassigned`，审核保持 pending。音频不写入图片台账。

另有三个12秒接缝试听，均从**实际解码的OGG**取最后6秒→开头6秒，接缝位于第6秒：[站内](audition/station-loop-boundary.mp3)、[探索](audition/explore-loop-boundary.mp3)、[救助](audition/rescue-loop-boundary.mp3)。MP3本身不作为采样精确循环载体，试听播放器也不能证明Unity循环无缝。

## 来源与生成

- 复用本机 ComfyUI 0.37.1 / Git `3f767e7f67bc587e88d6de6668eb424f725d4649`、RTX4080和官方原生节点；隔离8196服务、批次目录与队列，禁用custom/API nodes。未调用付费云、下载权重或安装依赖。
- Stable Audio Open 1.0 与 T5-base 的既有权重**本轮重新实测SHA-256**，与既有官方LFS来源登记一致，见[模型来源](provenance/models.json)和[运行环境](provenance/system-stats.json)。权重留在仓库外，不复制进项目。
- [计划/完整提示/种子](plan.json)、[API工作流](workflows/)、[成功历史及提交回执](history/)、[原始FLAC](raw/)完整保留。三作业分别为 `0ab554e5-bfd3-4505-ad3b-defc44c50323`、`12ffac8b-75d7-4f69-83bb-2d7f3e1a3842`、`eda2b1d8-583a-4c4c-92d1-e5ca9ab961d0`。47.6秒latent、60步/CFG5/dpmpp_3m_sde_gpu/exponential，导出前-18dB留余量；没有拿程序合成曲冒充模型输出。
- 完成后核对空队列、端口拥有者及命令行，仅停止本轮47724/99296进程，见[服务记录](provenance/service-run.json)。启动未就绪连接失败没有重复提交已接受作业；采样浮点边界warning保留说明，不宣称日志零warning。

## 后处理与验收

[process.py](process.py) 保留立体声，避开近静音/能量突变区选择33.5秒窗口，去直流后用1.5秒余弦等增益首尾交叉淡化，旋转接缝得到32秒。仅按增益匹配约-20LUFS，保留峰值余量，不用强限幅硬拉响度；输出44.1kHz/PCM16无损循环母带，再分别生成OGG和试听MP3。

这是接缝平滑处理，**不是自动配器/节拍量化或乐句首尾人工验收**。曲风、是否有非预期人声/音色、是否耐听、是否与现有SFX争抢、重复乐句是否自然均待用户试听。

本批临时线：单条OGG <=384KiB、三条 <=1MiB、-20LUFS ±1、true peak <=-3dBTP。现有项目资产规范给出BGM Streaming+压缩方向，未设上述数字；它们是本批制作线，不是微信官方限制或正式全项目预算。

实际完成：4/4处理单测；母带及OGG重新解码，有限数、非静音、立体声折单声道相容、无满幅削波、直流、时长、LUFS、true peak、接缝跃变与字节上限通过。三条OGG边界跃变分别-52.35/-65.99/-56.99dBFS，低于各自普通相邻采样跃变高分位，不能据此证明和声/拍点连续。完整分析见manifest，独立复验见[verification.json](verification.json)。

同一批原件两次完整后处理的manifest与全部音频哈希相同，manifest SHA-256：`34bda76e1d2130a9c3c1ac61aba2ae7a39f832d74dfcdd5abc87893197e8143d`。不承诺跨FFmpeg版本或硬件重新生成为逐采样一致。

## 接入与许可边界

用户接续授权后，已从 `masters/BGM_*_Placeholder.wav` 单次导入Unity，不使用OGG二次压缩。独立PlaceholderMusicCatalog由GameContent显式引用，不修改原六条音效Catalog或场景/Prefab；三曲计入Main首批静态依赖。Cue增益0.65，复用现有音量/静音与独立偏好存档。

Unity默认CompressedInMemory、Standalone覆盖Streaming，均32kHz立体声Vorbis/quality0.30/preload=true；Unity2022.3拒绝WebGL专属音频override，不能伪称已设置，浏览器使用默认候选设置，微信仍待验。实际独立未压缩AssetBundle含压缩音频合计988416bytes/965.3KiB（322368/328864/337184），含容器开销，各条<=384KiB且合计<=1MiB；仅成本测量，不新增运行时Bundle加载。原quality0.35合计1083648bytes超线，已拒绝同步并保留失败报告，未提高预算。详见[项目记录](../../docs/占位BGM制作与规格.md)，源OGG和Standalone测量都不是微信最终下载量。

Stable Audio的Community许可及T5重打包来源链继续待正式复核，沿用[已归档许可](../audio-basic-20261120/provenance/stable-audio-LICENSE.md)与[来源说明](../audio-basic-20261120/README.md#许可边界)。本地免费生成不等于无条件商用，未代用户注册或接受商业授权。Unity接入验证与提交状态按项目记录，微信/AOT/真机和完整听感验收仍未完成。

## 复现

```powershell
# 生成为恢复入口：已有回执只查询原作业，已有母带不重新提交。
powershell -NoProfile -ExecutionPolicy Bypass -File art-source/bgm-placeholder-20261123/start.ps1
node art-source/bgm-placeholder-20261123/generate.mjs
& 'C:/Users/Admin/ComfyUI-Installs/ComfyUI/ComfyUI/.venv/Scripts/python.exe' art-source/bgm-placeholder-20261123/process.py
& 'C:/Users/Admin/ComfyUI-Installs/ComfyUI/ComfyUI/.venv/Scripts/python.exe' art-source/bgm-placeholder-20261123/process.py --verify
& 'C:/Users/Admin/ComfyUI-Installs/ComfyUI/ComfyUI/.venv/Scripts/python.exe' -m unittest discover -s art-source/bgm-placeholder-20261123 -p test_process.py -v
```

不需要生成时只运行后两项，无需启动服务。后处理依赖既有ComfyUI Python、PyAV/numpy/scipy与内置FFmpeg；未安装soundfile，也没有为此新增依赖。重新出不同候选应开新批次，不删除本批回执重抽。
