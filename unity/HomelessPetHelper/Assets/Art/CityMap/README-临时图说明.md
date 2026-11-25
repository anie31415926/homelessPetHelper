# 城市地图美术（含历史生成记录）

生成日期：2026-11-17（画风迭代）/ 2026-11-18（场景+动物分层重构）。最初用于地图Editor白板，2026-11-20 起成为 Main 运行时依赖。**2026-11-26 清理：旧的 `T_CityPlace_*.png` 地标图、`T_CityMap_Forest.jpg` 森林承接图与 5 张 `T_Outing_*.jpg` 现场背景已删除；当前地标为 `StorybookLandmarks/` 与 `ValleyIcons/`，现场背景只保留 Parking + Treehouse 并加兜底。下方生成环境为历史来源，不是当前文件清单。**

## 2026-11-20 迁移说明

本目录保持`.meta`/GUID迁到`Assets/Art/CityMap/`，底图与地标由现有地图静态引用，Main依赖检查已覆盖。当前底图为`T_CityMap_Neighborhood.jpg`，地标为`StorybookLandmarks/T_Storybook_*.png` 与`ValleyIcons/T_Valley_*.png`；`CityMapLayers.json` 是早期 `T_CityPlace_*` 坐标的创作记录，随旧图清理已清空 layers，不是运行时布局加载器。唯一台账仍temporary/first-play，正式画风/许可/微信未验。完整流程与报告见仓库`docs/Main正式流程与资源迁移.md`。

## 当前方案：场景与动物分层（2026-11-18 起）

按用户方向重构为**两层**，好处是后期改场景、增删动物互不干扰：

1. **场景底图** `T_CityMap_Background.png`：一张俯视小城背景，只负责"地图长什么样"，不含任何动物。
2. **动物透明贴图** `Animals/T_Animal_*.png`：单只小动物的 RGBA 透明图，作为点位角标/图标叠加在底图上，可独立增删复用。

### 文件清单

| 文件 | 用途 | 来源 |
| --- | --- | --- |
| `T_CityMap_Background.png` (768×1344) | 俯视小城底图（彩色绘本风，留有大片草地可放点位） | `citymap_cnet_00005_.png` / seed 20261161 / ControlNet strength 0.42 |
| `Animals/T_Animal_CatCalico.png` (256²) | 三花猫透明角标 | `spr_animal_cat_calico_00001.png` / seed 20261153 |
| `Animals/T_Animal_CatOrange.png` (256²) | 橘猫透明角标 | `spr_animal_cat_orange_00003.png` / seed 20261181 |
| `Animals/T_Animal_DogWhite.png` (256²) | 白狗透明角标 | `spr_animal_dog_white_00001.png` / seed 20261152 |
| `Animals/T_Animal_Rabbit.png` (256²) | 兔子透明角标 | `spr_animal_rabbit_00002.png` / seed 20261174 |

> 动物贴图生成后经 BiRefNet 抠背景 -> PIL 对 alpha 取反（ComfyUI `SaveImageWithAlpha` 的 mask 语义与直觉相反，保存出来动物本体是透明的）-> 硬阈值(>40)求主体 bbox -> 裁切留边 -> 统一缩放 256×256。

## 生成参数

- 工具：本地 ComfyUI（`D:/comfy/ComfyUI`），API `http://127.0.0.1:8188`，用 `ComfyUI/.venv` 的 Python 启动。
- 基础模型：`NoobAI-XL-v1.1.safetensors`（SDXL，`D:/Bigmodel/checkpoints`）。

### 场景底图工作流（ControlNet 控结构）

- 画风目标：**"第一张"city_base_B 的彩色绘本俯视小城风**（用户选定），但解决它"建筑铺太满、没点位空间"的结构问题。
- 工作流：CheckpointLoaderSimple -> CLIPTextEncode(+/-) -> **ControlNetLoader(union-sdxl) + SetUnionControlNetType(`canny/lineart/anime_lineart/mlsd`) + ControlNetApplyAdvanced(strength 0.42, end 0.85)** -> KSampler(euler/normal, steps 30, cfg 7) -> VAEDecode -> SaveImage。
- 引导图 `D:/comfy/ComfyUI/ComfyUI/input/citymap_layout_guide.png`：用 `D:/comfy/draw_layout_guide.py`（PIL）自绘——一条主路 + 两条横路 + 散布建筑块 + 3 个粗椭圆空地（对应 3 个点位）。
- 调参经验：**strength 0.72 会把引导线稿几乎原样上色（变彩色矩形+椭圆圈，很丑）；0.42 结构引导恰到好处又给画风留空间**；对 seed 敏感，换 seed 易跑偏成抽象色块/漂浮小岛。最终 00005 是"小岛村庄"形态，道路结构未完全照搬，但大片草地正好叠加点位，画风与干净度都合格。
- 生成脚本：`D:/comfy/gen_citymap_v2.py`（`map` 子命令）。

### 动物透明贴图工作流（BiRefNet 抠图）

- 工作流：文生图（纯白背景 prompt）-> **LoadBackgroundRemovalModel(`birefnet.safetensors`) + RemoveBackground** -> SaveImageWithAlpha -> PIL 后期修正 alpha。
- 抠图模型 `birefnet.safetensors`（424MB，ModelScope 国内源下载，放 `ComfyUI/models/background_removal/`）。
- 出图不稳定项：NoobAI 对 "single/one" 单数约束弱，橘猫多次跑出两只/加白边底盘，靠加强 `exactly one single ... alone, one animal only` + 负提示 `two animals, pair, group, platform` + 换 seed 才出单只；"rabbit" 首次跑出两只带圆形底盘，换 seed 解决。
- 生成脚本：`D:/comfy/gen_citymap_v2.py`（`animals` 子命令，可跟 prefix 单独重出某只）。

## 遗留 / 待办

- **画风统一**：4 只动物里三花猫是厚涂大眼风、白狗/兔子是简笔平涂风、橘猫居中，小尺寸角标下差异可接受但非完全统一；正式版需统一（可固定画风基准 seed 或改用参考图引导）。
- **旧点位图**：`T_CitySpot_*.png`（停车棚/庭院/门廊，门廊为用户选定的厚涂基准）暂保留未删，作为画风参考；新方案下点位改用"底图空地 + 动物角标叠加"，这些整图不再直接使用。
- 正式美术需按 P5 门禁和[资源评估](../白板UI与资源管理.md)重新评审。

## 使用约束

- 这些图当前放在 `Assets/Art/CityMap/`，同时供Main和Editor预览引用；不是移动目录就自动成为最终美术。
- 图片来源：本地模型生成；模型及参考来源许可仍须正式审核，不能据“本地生成”推断商用许可已通过。
- 求助角标 / 点位选中态 / 未开放态等交互表现用 UI 叠加实现，动物贴图作为角标叠在底图上，不写死在图里。
- 替换正式图时保留语义文件名与 `.meta` GUID，只换贴图内容，减少 Prefab 重绑。

## 2026-11-21 林缘批次接入（当前）

用户认可并授权接入后，原地面与四张地标保持路径/GUID替图，新增公园、市场、河岸三张地标；这批 `T_CityPlace_*.png` 与森林承接图 `T_CityMap_Forest.jpg` 已于 2026-11-26 清理。当前底图为 `T_CityMap_Neighborhood.jpg`，12 处 Storybook 地标由静态 Prefab 以锚点摆放；名称/求助状态由独立 UI 显示。`CityMapLayers.json` 仅存早期创作坐标，layers 已随旧图清理清空，不是运行时加载器。

现场背景现只保留 `Assets/Art/Outing/T_Outing_Parking.jpg` 与 `T_Outing_Treehouse.jpg` 两张；`GameContent.asset` 按地点 ID 绑定这两条，其余地点由 `GameContent.GetOutingArtwork` 回退到 Parking，保证进入地点必有现场。开路和逆塔防复用同一张图。15项导入清单、哈希及来源见仓库 `art-source/forest-edge-20261121/unity-import.json`（历史快照，部分资源已删）；增量导入工具已随本轮清理移除。图均为temporary，许可/微信目标待验；当前状态和验证以 `docs/林缘美术Unity接入.md` 为准，下方旧文件名/画风和生成环境仅供历史追溯。
