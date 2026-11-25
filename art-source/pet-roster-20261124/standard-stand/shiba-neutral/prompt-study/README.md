# 柴犬前腿提示词对照

只改提示词：使用与上一轮宽遮罩版本相同的原侧图、遮罩、Qwen Image 2.1 INT8、负提示、种子 `2026112611`、Euler 24 步、CFG 4、denoise 1。原图和已存在的其它版本保留。A 强调站稳的结果与同侧脚底；B 强调短粗、连续的前臂轮廓和远侧只露脚趾。两份工作流保存完整提示词和输入哈希。

同尺寸全图及前腿放大见 `test-results/shiba-prompt-comfy/comparison.jpg`。A 修直了部分腿线，但腿腕仍偏窄；B 腕部比 A 自然，不过远侧前腿露得过多，近远前足的前后差仍像轻微迈步。这次两张均不选作绑定母图，不进入 Unity。评审 PNG 恢复了遮罩外原像素以消除 1 级舍入差，重新解码核对遮罩外零变化。

复现：本机 ComfyUI 8196，`node tools/pet-roster-standard-comfy.mjs shiba-pose-prompt` 输出 A，加 `--refine` 输出 B。作业分别为 `a6737c07-f3e1-44ea-b0a8-755e3a576b57`、`64fc05cf-b4c2-4f71-9fb2-298d10cb131b`。
