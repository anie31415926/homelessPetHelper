# 站内护理临时图

2026-11-18。只用于已授权的 Editor 护理试玩，不是最终资源，不批准正式发布或新的动物玩法。

## 来源与生成

- 本地 ComfyUI v0.37.1，API 仅监听 `127.0.0.1:8188`；没有调用云端生成、下载新模型或新增节点。
- 模型 `qwen_image_2.1_int8_convrot.safetensors`，编码器 `qwen3vl_8b_int8_convrot.safetensors`，VAE `qwen_image_2.1_vae_bf16.safetensors`。
- 工程 `T_Animal_DogWhite.png` / `T_Animal_CatOrange.png` 作为画风参考；用户的兔子截图仅用于目视确认方向，不把截图的棋盘格/Inspector 文案当画面内容。
- 正面全身坐姿、粗深色线条、小点眼睛、极简五官、浅淡平涂、少纹理、无字/无道具/无阴影、真实透明底。完整正负提示及图节点在 `Dog-workflow.json` / `Cat-workflow.json`，种子分别为 2026111801 / 2026111802；Euler / simple，24 步、CFG 4、shift 3.1。
- `*-master.png` 是生成的 768 方图，保留在 Unity Assets 外；`*-history.json` 为成功返回的完整历史。模型权重不进库，来源许可仍须正式审查。

## 派生与导入

生成器为 `tools/station-care-art.mjs`，后处理为 `tools/station-care-art-process.mjs`。需要本地 ComfyUI 和已有 sharp；`COMFY_URL` 可覆盖地址，`CODEX_NODE_MODULES` 可指定已有依赖位置。生成器不是自动资源更新器，重跑前须先归档/复核，不能覆盖已有已审图。

后处理仅缩至 512 方图、清除 alpha < 24 的几乎透明杂点，未裁边/改 pivot，未将 256 参考图放大成主图。128 方遮罩从同图透明轮廓内缩生成，额外避开中央五官；Unity 底左坐标与图一致。猫狗各一张基础图，清洁与杂毛由程序分层，不生成每个状态的整图。

| 运行图 | PNG 源文件 | 用途 |
| --- | --- | --- |
| `T_Station_DogTemporary.png` | 143537 bytes / 140.2 KiB | 小麦护理主体 |
| `T_Station_CatTemporary.png` | 113624 bytes / 111.0 KiB | 团团/豆豆共用护理主体 |

两张图合计 257161 bytes，两个遮罩另计 32768 bytes。PNG 源文件不是渠道下载或 GPU 内存；台账统一见 `art-source/resource-catalog.json`。

导入为 Sprite Single / FullRect，512 最大档，Read/Write 和 Mipmap 关闭，保留 alpha，当前 Editor 初筛使用未压缩设置。旧 `StationArt` 没有覆盖；护理实例改读新目录，原 Prefab 默认静态图暂留历史引用。

`*-light.png` / `*-dark.png` 为深浅背景边缘初筛图。已目视检查轮廓完整、无棋盘背景、没有明显白色底盘；狗线条比猫略带颗粒，正式版仍需统一笔触。护理热点与五官、局部污渍/梳理对齐通过桌面初筛，不代表人工画风定稿、商用许可或微信真机验收。
