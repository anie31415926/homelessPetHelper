# 山谷地图底图

2026-11-24，用户指定《游戏地图背景.png》作为 Unity 全屏外出地图的新底图。原件为用户提供的豆包生成图，1686×2486，SHA-256 为 `afa71d7d7a5ac1fb0b7b5fbf57c5365fb2e73d0d84508c30a0f49ed224e4479a`，保存在用户本机原位置，不覆盖、不上传。

`node art-source/city-map-valley-20261124/derive.mjs <原图路径> <输出路径>` 使用本机项目已有的 Sharp，从相邻林地取样并羽化修补右下角；仅修补可见的角标区域，无重新生成、裁切或拉伸。保留原始 TC260 AIGC XMP；导出 694×1024 JPEG，139,517 bytes。去除画面角标不代表修改来源、商用许可或渠道标识要求；发布前须核对授权与适用的生成内容标识要求。

实际运行路径 [T_CityMap_Neighborhood.jpg](../../unity/HomelessPetHelper/Assets/Art/CityMap/T_CityMap_Neighborhood.jpg) 沿用原 Sprite GUID 与导入设置；旧图通过 Git 和 [林缘地面候选](../forest-edge-20261121/terrain/candidate.jpg) 追溯。运行图仍是 Main 静态依赖，登记为 temporary / first-play；磁盘字节数不是微信下载量。
