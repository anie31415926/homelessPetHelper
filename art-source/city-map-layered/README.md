# 街区地图分层修正

日期：2026-11-19。用户明确地图应为「底图 + 可单独替换的交互地点图」，不是只把文字按钮从整图中分离。本批修正已有地图，不新建玩法或入口。

## 来源与处理

- 原图沿用 [原 ComfyUI 批次](../city-map-neighborhood/README.md) 的 `selected-master.png`，768×1344。原工作流、模型、seed、参考图及三次生成历史不变，由 `city-map.neighborhood` 统一登记，本次没有再次调用模型。
- `derivation.json` 记录原图坐标、四处图层包围框及轮廓；`tools/city-map-layers.mjs` 从原像素提取，不放大或重绘建筑。轮廓使用 4×4 子像素覆盖率抗锯齿，并只移除边缘连通的浅色草坪，保留小院内部地面。小院和救助站旁的树归属相应图层。
- 底图对旧建筑与投影区域取同高度草坪 RGB 中位数填补，外扩 14 px、外缘羽化 6 px，保留门廊附近可见道路。四栋非交互装饰民居仍在底图中；它们不是本轮的可点击地点。
- 地点 PNG 使用 256 色调色板、无抖动、保留 alpha；底图为不透明 JPEG，quality 82/mozjpeg。母图、合成图与白/深底边缘对照保留在此目录，只有运行图在 Unity Assets 中。

## 运行资源

| 图层 | 源图尺寸 | 源文件 bytes | 原图左上坐标 |
| --- | --- | --- | --- |
| 地面 `T_CityMap_Neighborhood.jpg` | 768×1344 | 28,592 | 0, 0 |
| 单车棚 `T_CityPlace_Parking.png` | 208×190 | 13,968 | 0, 722 |
| 居民小院 `T_CityPlace_Yard.png` | 354×211 | 25,520 | 414, 396 |
| 临街门廊 `T_CityPlace_Porch.png` | 250×234 | 17,432 | 514, 874 |
| 救助站 `T_CityPlace_Shelter.png` | 294×218 | 16,695 | 16, 936 |

合计 102,207 bytes，约 99.8 KiB；比原整图增加 45,814 bytes，换取独立替换能力。每张地点图在 60 KiB 暂行源文件目标内，不扩大 CityMap 模块总预算。源字节不等于下载量或运行内存；四张地点图 RGBA32 基础像素合计 947,224 bytes，仍待微信纹理格式与设备验证。

Unity 地点图为 Sprite/Single/FullRect、MaxSize 512、NPOT None、Clamp/Bilinear、alpha FromInput/Transparency，Read/Write 和 Mipmap 关闭；当前不压缩以保留小图边缘，无 WebGL 专属覆盖。底图复用原路径与 GUID，保持原导入策略。独立地图仍为 Editor 验证，不加入 Main 或 Resources，不引入新加载框架。

## 修改方法

1. 在 Prefab Mode 打开 `Assets/Prefabs/Panels/Panel_CityMap.prefab`，找到 `SafeArea/MapViewport/MapArt`。
2. `MapArt` 的 Image 只放地面。`Parking`、`Yard`、`Porch` 的 Image 各放一张透明地点图，其原 Button/事件绑定不变；`ShelterArtwork` 仅作救助站视觉，没有新增第四个外出入口。
3. 换地点时修改对应 Image 的 `Source Image`，不要把建筑再烘焙到底图。移动对应根节点的 RectTransform，建筑、名称和选择高亮一起移动；需要改尺寸时保持图片比例，并复核名称间距及热区。
4. 名称位于各节点的 `Nameplate` 下；文字和选中高亮不进入图片。地点图的遮罩/状态图以后也只替换这一层，不为每种状态另做整张地图。

`CityMapLayers.json` 是初始分层坐标的创作记录，不是运行时布局加载器。保存 Prefab 后不会被它自动覆盖；若正式调整初始布局，须同步记录及对应资产测试。一次性 `CityMapLayerUpgrade` 已执行；后续地点数量扩展时已移除这个过期工具及 MapLayers 命令，避免覆盖新绑定，后续不要重建 Prefab。地点增删和 Point Bindings 的现行步骤见[地图配置与扩展](../../docs/地图配置与扩展.md)。

## 复现与边界

在仓库根目录运行 `node tools/city-map-layers.mjs`；需可解析的 `sharp`，也可通过 `CODEX_NODE_MODULES` 指向已安装模块目录。脚本只覆盖本批确定的五张运行图、布局 JSON 和本目录三张对照图，不改 `.meta`、Prefab、台账或审核状态。正式手工替图后不要直接重跑旧派生流程覆盖新图。

全部仍为 `temporary / preview-only`，视觉、权利和目标平台审核 pending。当前是既有栅格图的分层修正，保留少量地面轮廓色与原构图，并非可无限放大的最终矢量资源。小图透明边缘和草坪补面已做截图初筛，不冒充用户美术验收、商业许可或微信首包测试。Unity 验证与后续边界见[实施 14.5](../../docs/城市地图与求助事件实现方案.md#layered-map)。
