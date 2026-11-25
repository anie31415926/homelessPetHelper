# 柴犬标准侧站母图

## 画风复修（当前）

[原图、前版与画风复修对照](storybook-comparison.jpg)右侧为 [选中母图](standing-storybook.png)，736×800。原侧图同时提供身份和画法；上一版站姿取边缘连通的纸底做简化，输入模型的是灰色剪影而非原有毛发、颜色或渐变。两张都是整图重画。选第二张：只露一眼，粗毛束、暖色外线、奶油胸毛和大块水彩更接近原图；臀部到近后掌保持连续，四爪可读。水彩色块分布和卷尾中心仍不与原图逐像素一致。

复现：本机 ComfyUI 8196，`node tools/pet-roster-standard-comfy.mjs shiba-storybook --refine` 生成选中原稿，不加 `--refine` 为第一张。两份工作流含提示、模型、种子、输入哈希及剪影来源，作业 ID 为 `083e6670-204f-4077-9dd2-e99593a53a96`、`572b52d3-af2f-4e79-a6e9-38e842a70c15`。两组全尺寸原稿在 `test-results/pet-sit-accepted/standard-stand/shiba-storybook*/`。选中原稿用 Sharp `modulate({saturation: 0.63, hue: -8})` 压低亮橘并偏回原图的柔桃色，不改结构、不拼图。对照仅作缩放浏览。本轮未抠透明、绑骨、改动画或导入 Unity。

## 前版整体重绘

[新旧站姿对照](redraw-comparison.jpg)右侧为 [整体重绘母图](standing-redraw.png)，736×800。用户反馈旧图臀部和后腿看起来没接上，本轮改为整图生成，不用局部蒙版或拼接。原侧图只认形象，旧站姿只参考四爪位置。两张中选第二张：背臀外轮廓连续进入近侧大腿、飞节和后爪，内侧靠软明暗区分腹部与大腿，远侧后爪仍可辨。第一张虽同样连贯，但露出远眼，侧面不够纯。

新版保留大圆眼、短吻、短腿和卷尾，毛色比原图略浅，笔触更细。两张对照裁切缩放只供目视。原图和上一版保留；新图仍是浅底评审母图，未做抠图、绑骨、动画或 Unity 导入。

复现：本机 ComfyUI 8196，`node tools/pet-roster-standard-comfy.mjs shiba-redraw --refine`，不加 `--refine` 为另一张。两份 `redraw*-workflow.json` 记录输入、哈希、模型、提示词和种子。作业 ID 分别为 `fe965110-11ef-49f3-8cae-ab69a216d545`（选中）和 `0e40c638-37ae-4521-8d9a-cd60d5a03197`；全尺寸未选图和作业历史见 `test-results/pet-sit-accepted/standard-stand/shiba-redraw*/`。

## 前版局部重绘

[原始迈步侧面与新版标准站姿](comparison.jpg)使用同一裁幅和比例对照。右侧的 [站姿母图](standing.png) 是本机 ComfyUI / Qwen Image 2.1 的局部重绘结果，728×768。原图的头脸、耳朵、背线和卷尾不重画：以同尺寸的身份输入为准，输出前 453 行 RGB 逐像素一致。

- 近前腿从胸下近垂直落地，不再前伸；四爪可辨。用户看图反馈近后腿与臀部连接读起来断开，前版仅保留作对照，不再作为绑定建议。
- 另一张是同提示词的全图重绘，站姿可读但重画了头脸、胸毛和色相，不选。当前母图仍为纸底 RGB，有淡地影；正式切层前需去地影和抠透明，按这张图重新标肩、肘、腕、髋、膝、跗及各掌接地；不沿用迈步原图的骨点。坐姿图只指导落座结构，不供换身换脸。
- 仅预览候选，未绑定、未做过渡、未替换工程或导入 Unity；源素材正式许可待核。

## 复现

使用已有本机 ComfyUI 8196 工作流：`node tools/pet-roster-standard-comfy.mjs shiba-neutral --refine`，不加 `--refine` 为全图对照。`masked-workflow.json`、`full-workflow.json` 保存模型、种子、输入哈希和提示词；脚本从原侧图重新裁切并生成纵向羽化遮罩。作业 ID 分别为 `27aa3815-55ca-4cfb-9c50-5ed6b512fe3f`、`75a2a57f-a956-4e1e-aeb1-d27af6aef6f2`；完整作业历史留在 `test-results/pet-sit-accepted/standard-stand/shiba-neutral-refine/` 与 `shiba-neutral/`。
