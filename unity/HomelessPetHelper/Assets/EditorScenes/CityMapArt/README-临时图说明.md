# 城市地图临时图（ComfyUI，非最终版）

生成日期：2026-11-17（画风迭代）/ 2026-11-18（场景+动物分层重构）。用途：支撑 A 组"地图最小呈现"的 Editor 白板验证。**全部为临时图，不作为最终美术；不进正式资源流程，不参与发布构建。**

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

- 这些图仅放在 `Assets/EditorScenes/CityMapArt/`，与 `StationArt` 同级，供 Editor 白板引用；正式主场景不引用。
- 图片来源：本地模型生成，无第三方商用素材；占位性质。
- 求助角标 / 点位选中态 / 未开放态等交互表现用 UI 叠加实现，动物贴图作为角标叠在底图上，不写死在图里。
- 替换正式图时保留语义文件名与 `.meta` GUID，只换贴图内容，减少 Prefab 重绑。
