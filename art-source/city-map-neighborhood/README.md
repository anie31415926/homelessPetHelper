# 街区地图临时图

2026-11-18，用户要求使用已有动画画风与 ComfyUI，替代地图白板。唯一条目：`city-map.neighborhood`，仍为 `temporary / preview-only`，人工观感、来源许可与微信交付均待验收。

## 生成与选择

- 本机 Comfy Desktop 安装目录为 `C:/Users/Admin/AppData/Local/Programs/Comfy Desktop`；实际后端为 `C:/Users/Admin/ComfyUI-Installs/ComfyUI/ComfyUI`，共享模型/输入/输出为 `C:/Users/Admin/ComfyUI-Shared/`。未修改 Desktop 配置、未下载权重。
- `tools/city-map-art.mjs` 通过本地 `127.0.0.1:8188` API 生成。UNET `qwen_image_2.1_int8_convrot.safetensors`，CLIP `qwen3vl_8b_int8_convrot.safetensors`，VAE `qwen_image_2.1_vae_bf16.safetensors`；Euler/simple、24 步、CFG 4、shift 3.1。完整图、提示、参考输入、历史均留本目录。
- 样式参考来自工程已有 `T_Station_CatTemporary.png`，居中放到 768×1344 白画布，未放大参考图。其来源许可仍沿用临时图待审状态，不因本次参考而变为获批。
- seed `2026111811` 的 `master.png` 房屋过密且线条细，未采用。`--refine` seed `2026111812` 的 `simplified-master.png` 更接近粗轮廓、平涂、稀疏布局，但模型夹带了参考猫。
- `--remove-character` seed `2026111813` 的 `clean-master.png` 虽移除猫，但改变全部布局，不采用。没有将其宣传为成功局部编辑。
- 选第二版布局。`tools/city-map-derive.mjs` 用同图顶部干净草坪替换猫的足迹，按相邻未改动草坪逐行匹配 RGB 均值并 10px 羽化，位置/区域见 `postprocess.json`；不移动建筑、道路、树或地标。`selected-master.png` 为修补后的母图，运行图为不透明 JPEG，quality 82 / mozjpeg，768×1344，无拉伸放大。

## 使用边界

地图为独立 Editor 试玩底图，地点按钮、选择状态与文字均由静态 Prefab 独立叠加，不烘焙动物、图标或任务状态。母图及流程不进入 Assets。暂未建立 Addressables、远程下载或正式首批资源清单；图集不是这张独立背景的强制要求。

导入使用 Sprite Single、MaxSize 2048、NPOT None、Read/Write 关闭、Mipmap 关闭、alpha None、Clamp/Bilinear、默认平台压缩质量 50。Unity 审计实测 768×1344、Standalone DXT1，源图 56,393 bytes（55.1 KiB），未在 enabled Main 的依赖内。不是已验证的微信纹理预设；RGBA32 参考像素量 4,128,768 bytes 不是下载字节或实际常驻内存。报告为 `test-results/unity/ResourceAudit-isolated-IllustratedMap.{json,md}`。

自动检查、截图和程序集构建仅验证当前临时接线；真人试玩、商业许可、微信转换包与真机内存/安全区尚未验收。未自动提交、推送或变更正式美术批准状态。
