# 绘本地图浮云与湖畔花海

蜜蜂和云雾由本批 `atmosphere.svg` 手工绘制，沿用原绘本的暖黄、浅奶油和灰绿。不重画底图或花海。运行图是 256×128 的透明 PNG，云在左 192×128 区域，蜂在右上 64×64；每块均有透明留边。

从仓库根派生：`node art-source/city-map-atmosphere-20261125/derive.mjs unity/HomelessPetHelper/Assets/Art/CityMap/T_CityMap_Atmosphere.png`。

Prefab 中的装饰层跟随地图拖动和裁切，不参与点击。花床直接复用原图与原锚点，仅对视觉网格做周期摆动，底部固定；蜜蜂沿花海内的路线绕飞。画面边缘的浮云缓慢移动。

区域迷雾和浮云是两层。迷雾用独立网格覆盖整张地图，通过当前片区轮廓挖出透明开口，边缘以渐变和低频纹理过渡，不复用云贴片遮挡点位。打开 Main 的地图后，在 Hierarchy 选 `MapRegionFog`，Play Mode 下切换 Inspector 的 `Preview Revealed` 可看左上码头与云朵乐园附近散雾和重新遮盖；正式默认关闭预览。此开关不修改点位开放、场景配置和存档。
