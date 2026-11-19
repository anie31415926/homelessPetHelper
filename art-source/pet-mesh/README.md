# 同源整图网格试验资源

2026-11-19。仅 `pet-mesh.html`，temporary / preview-only，不进Unity或正式首批。

## 来源与处理

- 唯一母图：`art-source/pet-rig/turnaround-master.png`。原ComfyUI模型、提示、seed和历史归 `pet-rig.turnaround` 唯一条目管理，本批未调用ComfyUI或任何云端服务。
- 用户要求从原图取材，不再分别生成头/身/腿再拼。本次取母图中整只侧面猫：x660/y60/w600/h680，所有身体区域相对位置保持不变。
- `tools/pet-mesh-process.mjs`：删除边界连通且最小RGB≥243的近白背景，保留封闭白毛；整图等比缩小至452×512，256色PNG/dither0.5。没有拆部件、重绘、补图、局部重排或新生图。
- `derivation.json` 保存母图SHA256、裁切矩形、处理步骤；`source-crop.jpg` 为对应原图裁切参考；深浅底复核图为 `edge-light/dark.png`。
- 唯一运行纹理：`public/pet-mesh/cat-side.png`，76254 bytes，约74.5KiB；RGBA32未压缩基础像素量925696 bytes，约904KiB，不代表实际GPU分配/微信交付量。当前Three.js默认纹理Mipmap及渲染缓冲另计。

## 使用边界

纹理贴在一张连续二维网格上，10根骨骼、每顶点最多4个归一化权重，三段尾骨、上下前腿及头颈/双耳局部联动。挂点和权重是本样本人工指定，不是自动识骨，也没有通用品种适配承诺。

整图网格不会补出被遮挡的腿或身体；抬前腿压力测试保留真实胸口牵拉，不用偷偷加一张隐藏补图掩盖限制。完整原图/零形变像素对照、动作和平台边界见 `docs/动物分层图片动作HTML试验.md` 的单图网格章节。

生成来源的权利审核和正式目标链路仍pending；本批只复核试验观感和可追溯性，不因此升为approved。
