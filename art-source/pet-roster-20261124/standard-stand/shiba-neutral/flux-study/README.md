# 柴犬标准站姿换模对照

[全图和前腿放大](comparison.jpg)：原图、上一轮 Qwen B、FLUX 仅文字、FLUX 加灰色前腿轮廓、FLUX 从局部形变图起步。均是预览，没有新绑定母图。对照比较可实际使用的工作流，不是同采样器、相同遮罩的单变量模型基准。

- 仅文字：近前腿仍斜跨，毛色更亮，远前腿有残边。
- 加灰色轮廓：脚略向内收，但上缘画成一条深色接缝，胸下有多余轮廓，仍不像自然承重。
- 从形变图起步：尝试固定回收后的脚位，并借原图看笔触，生成仍回到了原迈步轮廓。三张均不作为绑定底图。

本机 ComfyUI 使用 [Comfy-Org FLUX.2 [klein] 4B](https://huggingface.co/Comfy-Org/flux2-klein-4B) 的蒸馏 4B、FP4 文本编码器和 FLUX.2 VAE。官方分发 `5b3abca5c0dc1ecd4eaf3340868d09063fc1c965` 的三份 LFS SHA256 均逐文件核对；模型权重不入库。使用 `node tools/pet-shiba-flux-stand.mjs text guide shape`，Euler 四步、种子 `2026112611`、720×768，结果在 `test-results/shiba-flux-comfy/`。`shape` 依赖前轮忽略目录中的局部形变 PNG。三个工作流随目录留存，原始和局部合成结果分开保存；局部合成 PNG 复读检查确认遮罩外与同尺寸原图一致。
