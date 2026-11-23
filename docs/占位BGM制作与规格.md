# 占位 BGM 制作与规格

2026-11-23；本轮负责人 `01a2082b-0ed1-75a3-84f9-99a5ca93312d`。用户要求用ComfyUI做合适的占位音乐，注意体积/音质，明确不作为最终版本。

## 当前状态

已用本机既有Stable Audio Open原生工作流制作三条32秒候选：站内温暖木吉他、地图轻巧探索、救助专注但不紧张。方向为提示词意图，听感尚待试听，不把生成成功称为音乐审核通过。

入口、全长/接缝试听和完整来源见[本批README](../art-source/bgm-placeholder-20261123/README.md)。制作[manifest](../art-source/bgm-placeholder-20261123/manifest.json)保留原件/处理链冻结事实，当前Unity接入唯一登记为[integration](../art-source/bgm-placeholder-20261123/integration.json)。用户随后要求先接入并提交；三条仍temporary/非最终，但实际依赖已为first-play-static-dependency，不再把当前加载时机写成未分配。原图台账仅管理图片，本轮不改。

MainGameRoot显式持有MainMusicSession，按既有Flow阶段选音乐：Station/Care用站内，Map用探索，Approach/Negotiation/Rescued共用救助，不在清路到救助时重播。普通帧只作阶段/状态比较，不查找场景或新增播放器；Unity停止的Streaming曲目可能重新Unloaded，选曲变化时显式LoadAudioData一次，等异步就绪后重试，AudioService本身仍不做IO。GameplayPaused只由此会话汇总，前后台/失焦/禁用/外部打断保留各自原因，全部解除才恢复；静音保留音乐游标，离开Main释放会话。既有音乐/音效音量、保存/取消设置继续使用同一个MainAudioSession，不另建偏好。

独立PlaceholderMusicCatalog静态引用三曲并与原六音效合入同一个AudioService；原BasicAudioCatalog与六条精确断言不改。GameContent新增可选MusicCatalog绑定，不修改Main场景/Prefab/玩法/存档或其他会话文件。默认Cue增益0.65，用户原音量偏好不覆盖。

实际Importer默认CompressedInMemory、Standalone覆盖Streaming，Vorbis质量0.30、32kHz立体声、preload=true/loadInBackground=false。从PCM WAV单次压缩，不二次压OGG。Unity2022.3实际拒绝WebGL音频override，因此默认设置承担浏览器候选，不宣称设置了不存在的WebGL覆盖或渠道已支持Streaming。独立未压缩AssetBundle内含实际压缩音频：322368/328864/337184bytes，合计**988416bytes/965.3KiB**，含容器开销，未增运行时Bundle框架；三曲同为静态首批依赖，微信下载量/实际驻留仍待测。

## 本批规格

| 层次 | 规格及用途 |
| --- | --- |
| 原始母带 | ComfyUI 47.6秒/44.1kHz立体声FLAC，留工作流/种子/回执，仓库art-source追溯用，不进Assets |
| 循环母带 | 32秒/44.1kHz立体声PCM16 WAV，去直流、首尾交叉淡化、线性增益匹配；供后续单次Unity压缩 |
| 压缩候选 | 32kHz立体声Vorbis，目标80kbps；本批单条<=384KiB、三条<=1MiB |
| 音质检查线 | 目标-20LUFS ±1，true peak<=-3dBTP、无满幅削波、非静音/有限数/声道相容、接缝异常跃变检查 |
| 试听 | 全长96kbps MP3及OGG解码后末6秒→首6秒接缝MP3，仅便捷试听，不作运行时循环素材 |

这些数字是本批临时制作线，不是已批准的正式音频预算或微信渠道硬限制；项目现有[资产规范](conventions/topics/assets.md#s-8-1)要求BGM评估Streaming+Vorbis/ADPCM，没有规定以上数字。

实测OGG三条236926 / 252704 / 246902 bytes，合计**736532 bytes / 719.3KiB**；响度-20.00 / -20.01 / -19.97LUFS，true peak -8.64 / -5.41 / -5.96dBTP。无损母带、试听稿和原始FLAC分目录，不能把它们都计作运行首批，也不能用压缩字节代替解码内存。仅按PCM估算，32秒32kHz立体声约3.91MiB PCM16或7.81MiB float32；这不是Unity/微信实际测量。

## 后续验收

- 试听确认用途、无非预期人声/刺耳音色、乐句接缝自然，并与已有SFX混音；当前只测电平/采样连续，不宣称音乐上无缝。
- 本轮已使用现有MainAudioSession/AudioService和稳定Music Cue，没有新增常驻单例或每界面AudioSource；偏好和多原因暂停专项按下方证据读取。
- 本轮已从WAV母带单次压缩，Importer和就绪处理有实际验证；微信对应行为仍待转换包和目标机，不能仅套用Standalone结果。
- 三条已静态绑定独立音乐Catalog并计入首批；隐藏面板不等于延迟加载，Streaming不等于网络按需下载。未实施分包，不是正式美术/发行首批批准。
- Unity构建后的音频大小、真实下载/缓存、解码驻留、首次手势解锁、微信Streaming支持、设备静音、hide/show及广告打断另行实测；Editor可播放不等于微信通过。
- 正式发布前核对Stable Audio Community商业使用条件、T5来源链及最终作品使用许可；当前仅开发占位，不批准商业发行。

## 本轮验证

4/4处理单测；三份完整成功ComfyUI历史/种子/图/模型本轮哈希通过；WAV及OGG解码后时长/声道/削波/直流/响度/true peak/接缝/体积通过。相同原件重复处理的manifest及全部音频哈希一致，证据见[verification](../art-source/bgm-placeholder-20261123/verification.json)。manifest SHA-256为 `34bda76e1d2130a9c3c1ac61aba2ae7a39f832d74dfcdd5abc87893197e8143d`。

上述4项与manifest哈希是制作阶段证据，不代替后续Unity接入验证。生成服务早已停止，不动其他ComfyUI/Unity进程。

### Unity接入与实际提交内容

最终父提交已变为Loading会话独立提交的d7c6a80，因此追加核对父内容后全量EditMode **839/839**、Loading13+音乐6项真实PlayMode联调 **19/19**，均进程退出0；新22项BGM blob指纹未变。下面832/111为此前不含Loading父内容的固定快照基线，不直接冒称新父内容全量Play。机器登记同时保存839、先前111覆盖和最后19项联调；没有把其他站内/回访/贴图工作区一起提交。

本轮只暂存BGM与接入，HEAD+暂存树另导出到独立保存副本，不含其他会话的站内/Loading/回访/贴图。最终22项BGM Unity输入与Git暂存blob逐字节一致，范围指纹`589a2b220236de8e38113cc3a1ba3efe76fc3991073670209cb8fb94133adcf6`，可重跑[专用审计](../art-source/bgm-placeholder-20261123/verify_integration.py)，结果[unity-verification](../art-source/bgm-placeholder-20261123/unity-verification.json)。MainRoot/GameContent仅分离暂存BGM增量，其他人已有回访/保存改动保留未暂存。

- 实际提交快照EditMode **832/832**；PlayMode **110通过/0失败/1基线缺失跳过**，补齐既有动作A/B bundle后该唯一用例 **1/1通过**。合并覆盖111项，但如实保留原跳过XML，不称原全量零跳过；新BGM六项均在全量通过，含实际源越尾循环/单路九音源/真实站内→地图→清路→战斗→回站/暂停与失焦互不越权/静音与偏好重载/流式重新就绪/坏档无会话静音。
- 832全量对应最终坏档防护及六项测试，运行`EditMode-BgmStagedFinal`退出0；两个Play运行`PlayMode-BgmStagedFinal`与`PlayMode-BgmBaselineFinal`均Unity退出0，前者包装工具因原1项跳过返回1，后者1/1返回0。报告在上述登记，未降低跳过门槛或改报告。
- 原嵌套长路径的提交副本首次EditMode831/832，资源扫描受Windows/Mono路径长度失败；仅临时Q:映射缩短到同一副本后832/832，未改业务或扫描测试。统一入口仍failed：Editor重序列化既有空CityMap图集仅换行，导致保存输入字节不一致，不修改该无关资产/门禁，也不把手动固定副本通过冒称统一门禁通过。
- 共享工作区统一首轮EditMode855/855、PlayMode140/144，定位并修复本轮坏档空Session读取；固定副本仅同步本轮修正后144/145，余一项为站内菜单改版后的旧容量标签断言，原owner自行修正并释放后，保留副本单项回测1/1通过。该工作区快照包含其他未提交代码，不作为提交快照背书，旧统一failed保持不改。
- Unity实际压缩容器988416bytes，22输入与暂存精确比对通过；制作原件/图/回执/manifest字节在Git局部属性下保留，未夹带server数据库或模型。465脚本/37程序集共享源码检查0问题，提交树429/37为0问题。BGM新增无C# warning，现有Loading旧spritesheet弃用警告保留，不代修。

尚无独立人工Owner Review、人工音乐听感、微信/AOT/真机或正式商业许可验收；这是可替换占位开发提交，不作为发行批准，不推送。没有重新生成音乐、改变既有偏好或玩法，不修改UI层级/图片台账。
