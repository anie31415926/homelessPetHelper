# 紧凑关卡UI水彩控件

2026-11-22。用户要求减少UI留白，生成并接入素材。本批沿用已授权的本机ComfyUI与暖奶油/浅木/鼠尾草绿水彩画风。内置image_gen不可用，沿用用户已指定的ComfyUI；未调用OpenAI CLI或付费API、未下载模型或新增节点。

## 成品与用途

- [运行图](../../unity/HomelessPetHelper/Assets/Art/UI/T_UI_LevelControls.png)：512×272，71132bytes（69.5KiB），11切片，RGBA透明页；纸底板252²，主/次按钮240×64，8图标56²，共用一次纹理，九宫格可延展。
- [切片配置](../../unity/HomelessPetHelper/Assets/Art/UI/LevelControlsSlices.json)：边框36/按钮22和18源像素，保留4px间隔；不含文本、交互或全屏界面烘焙。
- [深底边缘预览](edge-review.jpg)、[派生依据](derivation.json)、[派生工具](derive.mjs)。母图不直接导入Unity。
- [提示词全集及参考](jobs.json)、各项paper/primary/secondary/symbols目录内master.png、workflow.json、submission.json、history.json保存真实作业依据。

## 生成来源

本地ComfyUI0.37.1、Python3.13.12、PyTorch2.10.0+cu130、RTX4080，[实际环境](system_stats.json)。既有qwen_image_2.1_int8_convrot.safetensors，qwen3vl_8b_int8_convrot.safetensors，qwen_image_2.1_vae_bf16.safetensors。TextEncodeQwenImage21以形状参考作为第一输入，室内风格作为第二输入，resolution=0，AuraFlow shift3.1，24步，CFG4，Euler/simple，denoise1。种子2026112201–2026112204，4个真实作业均成功。

纸底板与按钮尺寸/形状参考由prepare.mjs制作，用于控制输出，未冒充生图；风格参考是用户提供的[室内水彩参考](../map-style-20261121/references/station-style.jpg)。不复制其他游戏资产，参考作者和模型的商用条件未重新验收。

run.mjs沿用前批提交/收集方式；已提交作业使用collect恢复，不重复排队。只启动本任务ComfyUI进程36260，作业完成确认队列空闲、卸载模型后已停止，不修改用户其他服务。

## 后处理与导入

底板/按钮只从边界连通区域去除近白背景（最小RGB通道≥244），不掏空浅色纸中心。图标去白并生成可着色alpha，低于24/255的覆盖舍去，清除稀薄杂点；裁非零alpha边界、Lanczos缩到运行尺寸、合为256色PNG。完整源hash、裁切、单色处理、目标位置见derivation.json。原母图像素另存，图标仅保留形状与纹理透明度。

Unity用既有2D Sprite Editor数据提供器导入多Sprite，显式边框、无NPOT缩放、MaxSize512、Bilinear/Clamp、关闭读写及mip，当前不压缩RGBA32。导入基础像素参考557056bytes（544KiB），不把71KiBPNG当显存或微信包体。没有另建SpriteAtlas，避免小页再包装成更大页。

按现有Prefab Mode做增量绑定，保留业务字段与原图GUID。[导入来源及修整](../../test-results/compact-ui/prefab-import.json)，[Unity只读审计](../../test-results/compact-ui/image-resources.json)确认Main静态依赖此页；未新增运行时加载框架，全部记temporary/first-play。唯一台账仍为../resource-catalog.json。

台账检查无缺失/漏登记。Main当前22项图片源合计1689.2KiB，超过原1536KiB源规划线153.2KiB；预算未提高，微信下载未测，维持首批draft与许可/目标验证pending。最终UI回归证据见[紧凑UI记录](../../docs/紧凑UI与水彩控件.md)。
