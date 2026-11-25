# Qwen 纯文生图、参考图与缓存核对

四图对照在 `test-results/shiba-qwen-fresh-comfy/comparison.jpg`，依次为原图、旧视觉参考、纯文生图、新参考图加 VAE。纯文生图更接近幼犬比例，但露远侧眼、偏橙。参考图加 VAE 后更接近原角色和正侧面，仍不等于原图像素保真，未定绑定母图。旧版本提示词不同，不将全部改善归因给 VAE；本轮两个新图同提示、同采样节点，空 latent 尺寸按有无参考自动决定。

旧两条历史的 `execution_cached.nodes` 均为空，解码 RGB 有 530,232 / 588,800 个像素不同。并非复用同一张图。旧整图节点有图像输入但未接 VAE，走视觉编码器条件；旧局部编辑另有 `VAEEncode → SetLatentNoiseMask`，已经在做遮罩图生图。新节点补 `vae`，将原图也编成参考 latent；这仍不同于从原图 latent 起步的通用 `VAEEncode → KSampler`。

新独立实例带 `--cache-none`，节点 `QwenImage21Cache` 设 `off`。使用 `node tools/pet-roster-standard-comfy.mjs shiba-qwen-text shiba-qwen-ref-vae`。纯文本图无 `LoadImage` 节点；两组提示词和配置见随档工作流。作业分别为 `c51b4560-a32a-428c-994b-63bf049a75d3`、`3154bba8-7a34-424c-9cef-26b3869a5689`。全尺寸结果在 `test-results/pet-sit-accepted/standard-stand/shiba-qwen-{text,ref-vae}/master.png`。
