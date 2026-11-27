# ComfyUI 输入

- plate-512.png：512×512 干净底稿，来自 T_Station_ShibaMotion 第 0 帧（126px lanczos 放大），透明底。所有重绘都以它为基准，不要另生成底稿。
- mask-neck-back.png / mask-belly.png / mask-hip-back.png：补绘用的 inpaint mask，白=重绘，黑=保留，已在标注框外扩 8px。
- masks-preview.png：三块 mask 位置核对图（红框）。

用法：ComfyUI 里 mask 节点接 GrowMask 6–8px + MaskBlur 6，denoise 0.45–0.6，尺寸严格 512×512，别开高分修复（会改尺度）。
