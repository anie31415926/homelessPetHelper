# B版布局停车棚背景

> 2026-11-26 清理注记：本批次 workflow / history 中引用的 `T_Station_CatTemporary.png` 等临时资源已删除，此处保留为历史快照，不是当前资源清单。

2026-11-18，用户允许为 B 版操作基线用本地 ComfyUI 制作必要图片。唯一台账 ID：`rescue.bicycle-shelter.layout-b`，temporary / preview-only；不代表正式美术或商用来源通过。

## 来源与结果

- 本机 ComfyUI `127.0.0.1:8188`，Qwen Image 2.1 INT8 ConvRot；CLIP `qwen3vl_8b_int8_convrot.safetensors`，VAE `qwen_image_2.1_vae_bf16.safetensors`。
- 初次 seed `2026111821`，24步、CFG4、Euler/simple、shift3.1；完整 API 图在 `workflow.json`，执行依据 `submission.json` / `history.json`。
- 参考现有 `animal.cat.station-care` 的独立猫图，在768²白底中央贴300²角色作为画风参照。模型忽略“不要动物”约束，首图夹带一只猫，因此首图不能直接作为独立背景。
- 第二次 seed `2026111822` 尝试去猫；`empty-workflow.json`、`empty-submission.json`、`empty-history.json` 保留。初始化耗时明显异常，定向取消本任务，不把中断当成成功，没有第二张成图。
- 最终保留首图地标，以同高度左侧空铺面克隆覆盖中心夹带猫，边缘18px羽化；`tools/layout-b-art-clean.mjs` 及 `postprocess.json` 记录像素坐标、输入输出和JPEG参数。没有挪动屋顶、自行车、灌木，没有扩大分辨率。

## 文件与使用

`clean-master.png` 是清理母图，`clean-backdrop.jpg` 是选定的768×768 JPEG quality84 mozjpeg。`master.png` / `backdrop.jpg` 是夹带猫的生成记录，**不可接入显示**。`style-reference.png` 仅为参考拼板。

B版 Web 引用 `clean-backdrop.jpg`，动物单独复用已有透明512²猫，文案、图标、按钮均由真实DOM绘制。背景没有烘焙动物、文字和UI。Web阶段所有新图在Unity外；随后用户允许移植Unity，已按原字节导入 `unity/HomelessPetHelper/Assets/EditorScenes/RescueCaseArt/T_Rescue_ShelterTemporary.jpg`，该路径为台账唯一当前runtime，外部JPEG保留reference。原动物Sprite直接复用，没有复制或改站内绑定，也未接加载系统或正式首批。

已查看清理图与组合画面，中心无残留角色；背景比前景更柔和，画风统一性仍由用户反馈决定。视觉、模型/参考素材商用许可、微信/真机目标验证仍 pending。图片台账检查结果见 `test-results/resource-catalog/`，不是微信下载验证。

本任务临时启动的 ComfyUI 后端在队列空闲后已停止；原有用户服务未动。Figma MCP Starter额度限制导致B版未同步，当前交互稿 `layout-b.html`。
