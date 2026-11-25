# 正式关卡设计：ComfyUI 界面构图参考

> 2026-11-26 清理注记：本批次 workflow / history 中引用的 `T_Station_CatTemporary.png` 等临时资源已删除，此处保留为历史快照，不是当前资源清单。

2026-11-22。用户明确要求使用本机 ComfyUI，沿用项目清新手绘/水彩画风。本批只生成整体界面参考，保存在 Unity 外，不替换现有图片、Prefab、路线或交互。生成前已在唯一[资源台账](../resource-catalog.json)登记两个 planned 条目。

## 看图

| 图 | 用途与检查 |
| --- | --- |
| [救助参考](rescue/concept.jpg) | 场景满屏、自然路径与场内道具、顶部角落 HUD、底部轻薄操作页；保留作候选 |
| [清障初稿](clearing/concept.jpg) | 手绘质感和薄工具栏可参考，但中央过空、物件关系不明确；保留生成历史 |
| [清障修订](clearing-v2/concept.jpg) | 针对初稿中央空地过多，要求三条可读的石径与分区阻挡；具体目视验收见下方本批结果 |

这些是大模型生成的概念图，不是运行截图。模型可能生成错误字、重复图标、额外按钮或不符合规则的箱/桥；不能照着图片直接硬编码交互。文字、热区、路线、道具仍应由现有 UI 与场地数据生成。猫的头身比例和路径拓扑也需按项目真实表现再校正。

## 实际生成依据

- ComfyUI 后端版本 0.37.1，现有本机环境；未安装新模型、未调用 OpenAI API。
- 模型：`qwen_image_2.1_int8_convrot.safetensors`；CLIP：`qwen3vl_8b_int8_convrot.safetensors`；VAE：`qwen_image_2.1_vae_bf16.safetensors`。没有 LoRA、ControlNet。
- `TextEncodeQwenImage21` 读取两张参考，`resolution=0`，参考画布 768×1664；`ModelSamplingAuraFlow shift=3.1`，KSampler 24 步、CFG 4、Euler / simple、denoise 1。
- seed：救助 `2026112291`、清障初稿 `2026112292`、清障修订 `2026112293`。完整正负提示与 API 图在各目录 `workflow.json`，实际提交响应在 `submission.json`，真实完成状态和输出在 `history.json`。
- 每次原始输出保存为 `master.png`；Pillow RGB 转 JPEG、quality 93，得到同尺寸 `concept.jpg`，未裁切或缩放。文件哈希由台账检查读取；参考来源与处理见 [provenance.json](provenance.json)。

参考 1：[layout-reference.png](layout-reference.png)，来自上一轮实际 Unity 救助截图，只约束构图，390×844 放大到 768×1664，非生成成图。参考 2：项目现有小院场景 `Assets/Art/Outing/T_Outing_Yard.jpg`，只提供画风、调色与地点线索。参考图片已有临时来源记录，模型和底层参考的许可并未因再次生成自动通过。

完整提示见[救助 workflow](rescue/workflow.json)、[清障 workflow](clearing/workflow.json)、[清障修订 workflow](clearing-v2/workflow.json)。提示核心为：清新彩铅细线与轻水彩，场景连续满屏，大部分高度可游玩，自然比例动物，顶部中央避挖孔，角落紧凑信息，薄底部卡牌/工具栏，提示远离当前可点内容。修订只重点补充可读路径与分区杂物，避免大块空白。

## 过程与边界

`generate.py` 生成前两张，`refine.py` 只提交第三次修订；它们会调用本机 API 并消耗本机 GPU，重新运行会新增作业，不用于常规验证。`start.ps1` 在没有监听 8188 时启动本批自有后端，进程信息与日志只作本机运行记录。收尾只在队列为空且身份匹配后停止本批启动的进程，不停止用户已有服务。

三次 API 作业全部成功，均为 768×1664。目视选择救助第一版与清障修订：场景占比大、底栏轻薄、选箱仍可见，清障修订补足了中央路径和分区。但两张图的路径未严格组成配置中的三路，猫仍略偏幼态；救助稿有生成文字和重复角落图标，清障稿存在美术意义上的额外泥草。均只用于构图，不作为可直接落地的关卡图。所有候选保持 `candidate / preview-only`，未正式审核或接入 Unity，不能当作真实界面、玩法验证、首批资源或微信下载验收。


台账检查：本批完整生成后运行 `node tools/resource-catalog.mjs`，退出 0，无 error / MissingFile / UnregisteredImage。三项新登记均保持 preview-only；图片未加入 Main 首批。构图参考 PNG 为 1081.0KiB，超其 512KiB 的早期计划值，保留 ImageOverPlan 提示，未提高预算或将其当运行图；既有首批草稿、源规划超额和许可待审核提示继续保留。本轮服务仅在队列为空并核对进程身份后停止了本批自启的 101116 / 74540，未停止用户服务。
