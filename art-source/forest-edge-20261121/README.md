# 林缘社区 ComfyUI 候选批次

2026-11-21。用户认可布局后，要求使用本机 ComfyUI 按所发图片画风生成本批。16 项候选均已落盘，状态为 `candidate / preview-only`；未导入 Unity、未替换现有图或 Prefab、未提交推送。场景、地标和整体展示分别保存，整体预览不作为分层地图的运行底图。

## 直接看图

- [首图分层合成效果](assembled.jpg)：上部森林、右侧溪流、六处地点与底部救助站。
- [下一张森林](forest-bridge/candidate.jpg)：入口直木桥、同侧溪流及更深林地；仅视觉候选，未确定新地点/开放条件。
- [六处现场](scenes-review.jpg)：单车棚、小院、门廊、公园、市场、河岸。名称仅在评审页，不在实际背景里。
- [独立地标](landmarks-review.jpg)、[深浅底边缘对比](edge-review.jpg)。
- [16 图下载包](forest-edge-candidates.zip)：只含当前候选、放置参数及索引，不含大母图。

## 当前图片

| 用途 | 当前文件 | 像素 | 文件 bytes |
| --- | --- | --- | --- |
| 林缘社区整体构图候选 | [overview](assembled.jpg) | 768×1024 | 111,693 |
| 林缘社区分层地面底图候选 | [terrain](terrain/candidate.jpg) | 768×1024 | 78,990 |
| 林缘地图独立单车棚候选 | [landmark-parking](landmark-parking/candidate.png) | 206×165 | 20,763 |
| 林缘地图独立居民小院候选 | [landmark-yard](landmark-yard/candidate.png) | 249×187 | 22,051 |
| 林缘地图独立临街门廊候选 | [landmark-porch](landmark-porch/candidate.png) | 235×220 | 24,631 |
| 林缘地图独立林缘公园候选 | [landmark-park](landmark-park/candidate.png) | 265×105 | 43,038 |
| 林缘地图独立市场后院候选 | [landmark-market](landmark-market/candidate.png) | 198×155 | 17,810 |
| 林缘地图独立河岸步道候选 | [landmark-riverside](landmark-riverside/candidate.png) | 140×105 | 20,211 |
| 林缘地图独立救助站候选 | [landmark-shelter](landmark-shelter/candidate.png) | 204×136 | 57,590 |
| 单车棚地点内背景候选 | [scene-parking](scene-parking/candidate.jpg) | 768×1024 | 85,728 |
| 居民小院地点内背景候选 | [scene-yard](scene-yard/candidate.jpg) | 768×1024 | 72,949 |
| 临街门廊地点内背景候选 | [scene-porch](scene-porch/candidate.jpg) | 768×1024 | 63,075 |
| 林缘公园地点内背景候选 | [scene-park](scene-park/candidate.jpg) | 768×1024 | 53,638 |
| 市场后院地点内背景候选 | [scene-market](scene-market/candidate.jpg) | 768×1024 | 110,233 |
| 河岸步道地点内背景候选 | [scene-riverside](scene-riverside-clean/candidate.jpg) | 768×1024 | 50,907 |
| 后续森林衔接地图候选 | [forest](forest-bridge/candidate.jpg) | 768×1024 | 136,291 |

16 项合计 969,598 bytes（946.9 KiB），包含整图展示副本，不能当成游戏运行下载量。背景和整体图每张预算 200 KiB、独立地标每张 60 KiB，全部在原计划内。完整尺寸/字节/SHA256 见 [candidate-manifest.json](candidate-manifest.json)，唯一资产归属及审核仍在 [资源台账](../resource-catalog.json)。

本批采用 768×1024 内容画布：首图只画地图内容，顶部状态与底部详情由 UI 独立提供；现场留出大面积中央操作区。不是把 9:16 手机整屏和按钮一起烘焙成图。此尺寸是候选起点，接入时须按实际显示/安全区重新核对，不代表已有像素适配验收。

## 来源与生成依据

- 用户 [户外参考](../map-style-20261121/references/forest-style.jpg) 提供浅黄绿水彩、暖木材、森林与溪水语言；[室内参考](../map-style-20261121/references/station-style.jpg) 提供奶油色、淡木色与生活气息。作者与授权条件沿用 [参考归档说明](../map-style-20261121/README.md)，仍待确认。
- 用户已认可的 [原布局示意](../map-style-20261121/layout/forest-edge-layout-v1.svg)，本批去掉标签/UI 后使用 [布局输入](layout-reference.png)。没有照搬参考中的蘑菇屋和花边。
- 实际使用本地 ComfyUI 0.37.1、Python 3.13.12、PyTorch 2.10.0+cu130、RTX 4080。环境快照见 [system_stats.json](system_stats.json)。没有调用 OpenAI API、下载模型或安装新节点。
- 模型：`qwen_image_2.1_int8_convrot.safetensors`；文本编码器：`qwen3vl_8b_int8_convrot.safetensors`；VAE：`qwen_image_2.1_vae_bf16.safetensors`。文件名是实际本地版本标识，未伪造权重版本/哈希，模型使用条件未重新验收。
- `TextEncodeQwenImage21` 输入参考图 + VAE，resolution=0（第一张输入控制画布）；AuraFlow shift=3.1；Euler / simple；24 steps；CFG=4；denoise=1；每项实际 graph、提示、输入、seed、提交 ID 和后端 history 都保留。初始整体尝试后将救助站单独生成，修订河岸多余建筑和森林直桥，共 12 次成功作业。
- 提示词全集：[jobs.json](jobs.json)；运行与收集脚本：[run.mjs](run.mjs)。脚本拒绝重复提交及占用中的队列；重收集已有任务用 `collect <slug>`，不得再次 `run`。工作流是 ComfyUI API graph，复用须按本机模型/节点版本核对，不能仅靠 seed 保证逐像素相同。

| 作业 / 完整工作流 | seed |
| --- | --- |
| [overview](overview/workflow.json) | 2026112101 |
| [scene-parking](scene-parking/workflow.json) | 2026112111 |
| [scene-yard](scene-yard/workflow.json) | 2026112112 |
| [scene-porch](scene-porch/workflow.json) | 2026112113 |
| [scene-park](scene-park/workflow.json) | 2026112114 |
| [scene-market](scene-market/workflow.json) | 2026112115 |
| [scene-riverside](scene-riverside/workflow.json) | 2026112116 |
| [forest](forest/workflow.json) | 2026112120 |
| [terrain](terrain/workflow.json) | 2026112102 |
| [shelter-building](shelter-building/workflow.json) | 2026112103 |
| [scene-riverside-clean](scene-riverside-clean/workflow.json) | 2026112131 |
| [forest-bridge](forest-bridge/workflow.json) | 2026112132 |

每个作业目录均有 `master.png`、`workflow.json`、`submission.json`、`history.json` 和 JPEG 候选。河岸旧版、森林旧版作为参考保留，当前选择以台账为准。共享输入/输出在 `C:/Users/Admin/ComfyUI-Shared/{input,output}/forest-edge-20261121/`；仓库内已保留本批全部当前图与实际生成记录。

## 分层与后处理

[derivation.json](derivation.json) 和 [derive.mjs](derive.mjs) 记录完整轮廓、坐标与处理；每个 `landmark-*/placement.json` 给出源区域及合成位置，原点为 768×1024 地面左上角。地标轮廓从整体母图同源裁出，保留接触地面，不放大；公园草地边沿局部羽化。未量化 RGBA 派生母图保存在各地标 `master.png`。

救助站使用另一次模型生成，先从画布边缘连通去白（最低通道 241、通道差不超过 21），裁切实际区域 528×353，再缩至 204×136，放置于 (280,857)。这是浅色底后处理抠图，非模型原生 alpha；屋顶/墙面与深浅底边缘已查看，正式接入前仍需复核。

单车棚、小院、门廊、市场的无损 PNG 初稿超过 60 KiB，保留尺寸、采用 256 色/dither=0.5 候选，未量化版本仍在 master；其余三地标保持原 RGBA。合成图使用当前候选像素，JPEG quality=88 / mozjpeg；不拿另一组未登记图片冒充同层合成。

`node art-source/forest-edge-20261121/derive.mjs` 重新派生并更新台账；`--images-only` 仅出图。`review.mjs` 使用现有 Chrome/Playwright 导出中文评审页，标题和名称仅在评审页。任何重生成后重新核对台账与尺寸/字节，不能沿用旧 SHA256 或视觉结论。

## 本轮检查与接入边界

已逐张查看母图和修订图，检查画风、意外人物/动物/文字、操作留白，以及地标深浅底边缘和叠回地面的效果。河岸误生的两间小屋已移除；森林桥已改为直桥，与首图共享木色/栏杆样式。两图是视觉承接候选，不是无缝拼图，桥的尺度和接缝没有经过 Unity 实际切图验收。地点现场中的台阶、门、车架、箱堆均属美术内容，不自动变成可操作物或新的障碍规则。

执行 `node tools/resource-catalog.mjs` 通过，未出现缺失/未登记图或本批预算超额。仍保留此前 14 条提醒：首批范围草稿、旧坐姿图超计划、12 项旧资源未批准；正式首批仍为原 12 项 / 1067.9 KiB，未把本批候选计入首批。34 个先前台账条目及总预算未改，9 份既有工作区文件哈希一致，见 candidate-manifest。没有运行 Unity/微信验证，本轮没有工程接入变更。

`review.visual/rights/target` 均保持 pending；本轮检查不替代用户观感、来源许可或微信目标验证。森林地点、切换、求助刷新、探索发现和寻宠交接仍按玩法讨论推进，不因出图视为已实现。

4张预览和16图ZIP已通过当前 Botmux 会话投递，主消息 `om_x100b7f2d78bca0a4cfbbd71278deffb`、附件 `om_x100b7f2d7844e4a0ceb067b5018e87a`，见 [投递回执](botmux-delivery.json)。队列清空后仅关闭本轮启动的后端，端口8188已停止；不影响已有Desktop配置。接续如需重生成，先确认本机服务及其他队列，不能依赖本轮临时进程仍在运行。

## 用户认可后的 Unity 接入（2026-11-21）

用户明确“还挺不错，可以接入到工程里”。本轮从候选中选择15项导入：地面、7地标、6现场和森林承接；整体预览不导入。完整来源/目标/尺寸/SHA256在[unity-import.json](unity-import.json)。原有5个图片GUID和3个面板Prefab GUID保持；使用Unity隔离增量编辑，而非重建面板。

实际接入、试玩和最新验证见[林缘美术Unity接入](../../docs/林缘美术Unity接入.md)。台账将实际静态引用的14项记temporary/first-play，已替换的5条旧地图资源转retired；森林导入但Main不引用，仅作preview-only预留。用户观感认可记visual accepted，rights/target仍pending；不是正式发布批准。前文“未导入/候选”是生成阶段事实，以本节和运行资产为最新状态。
