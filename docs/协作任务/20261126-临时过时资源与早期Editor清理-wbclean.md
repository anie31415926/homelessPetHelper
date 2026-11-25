# 临时/过时资源、预制体悬空引用与早期 Editor 脚本清理

- 任务 ID：20261126-临时过时资源与早期Editor清理-wbclean
- 负责人：wb-clean-1126（WorkBuddy 会话）
- 最近更新：2026-11-26T02:36:00+08:00
- 状态：完成
- 用户授权：用户 2026-11-26 明确要求清理临时资源、过时资源、过时编辑器代码、过时代码（举例 `T_Station_CatTemporary`、`T_Station_DogTemporary`），并补充「预制体、文档也先统计」。用户已手动删除 13 组图片/mask（含 .meta）。经统计与四轮确认，用户拍板：
  1. **动物形象统一**：「因为这里还有别的地方形象不统一，需要全部改成正式形象，所以旧的就直接清理即可，由于现在还在开发期，存档不用过于在意」→ 把所有显示临时护理图的地方改绑正式救助动物形象（`T_Pet_original.png` 猫 / `T_Outing_DogNatural.png` 狗），清理 4 个 `T_Station_*Temporary*` 资源；开发期允许简化 mask 存档校验。
  2. **现场背景**：只保留 `T_Outing_Parking.jpg` + `T_Outing_Treehouse.jpg` 两张，其余 5 张删除，运行时加兜底。
  3. **Editor 代码**：只清理「和当前游戏流程不一致的早期 editor 脚本」，不做全量一次性工具清除。
  4. **文档**：正文文档改表述；art-source 台账/历史快照保留但加注。
  5. **ValleyIcons**：最后一轮用户明确「ValleyIcons 里的先不删了」→ 12 张全部保留（实测其中 7 张被 `Panel_CityMap.prefab` 的 `_detailArt`/`_stationIcon` 活绑定，删了会留悬空图标）。
  - 用户补充：交互点内场景资源保留一张老图作兜底即可。

## 写入范围

```collaboration
{
  "sessionId": "wb-clean-1126",
  "writePaths": [
    "docs/协作任务/20261126-临时过时资源与早期Editor清理-wbclean.md",
    "docs/协作执行记录/20261126-临时过时资源与早期Editor清理-wbclean/",
    "unity/HomelessPetHelper/Assets/Settings/Game/GameContent.asset",
    "unity/HomelessPetHelper/Assets/Scripts/Game/GameContent.cs",
    "unity/HomelessPetHelper/Assets/Scripts/Game/Station/StationArtwork.cs",
    "unity/HomelessPetHelper/Assets/Scripts/Game/Progress/MainResidentCodec.cs",
    "unity/HomelessPetHelper/Assets/Scripts/Modules/Station/UI/DirtSurfaceView.cs",
    "unity/HomelessPetHelper/Assets/Scripts/Editor/MainSceneAuthor.cs",
    "unity/HomelessPetHelper/Assets/Prefabs/Panels/Panel_CityMap.prefab",
    "unity/HomelessPetHelper/Assets/Prefabs/Panels/Panel_RescueCase.prefab",
    "unity/HomelessPetHelper/Assets/Prefabs/Panels/Panel_StationWhiteboard.prefab",
    "unity/HomelessPetHelper/Assets/Prefabs/Panels/Panel_StationLoop.prefab",
    "unity/HomelessPetHelper/Assets/Prefabs/Panels/Panel_Approach.prefab",
    "unity/HomelessPetHelper/Assets/Prefabs/Panels/Panel_SiegeTrial.prefab",
    "unity/HomelessPetHelper/Assets/Art/Station/",
    "unity/HomelessPetHelper/Assets/Art/Outing/",
    "unity/HomelessPetHelper/Assets/Art/CityMap/CityMapLayers.json",
    "unity/HomelessPetHelper/Assets/Art/CityMap/README-临时图说明.md",
    "unity/HomelessPetHelper/Assets/Settings/Atlas/UiAtlasGroups.json",
    "unity/HomelessPetHelper/Assets/EditorScenes/StationArt/",
    "unity/HomelessPetHelper/Assets/EditorScenes/RescueCaseArt/",
    "unity/HomelessPetHelper/Assets/Scripts/Editor/RuntimeMigration.cs",
    "unity/HomelessPetHelper/Assets/Scripts/Editor/RescueCase/RescueCompactLayoutUpgrade.cs",
    "unity/HomelessPetHelper/Assets/Scripts/Editor/StationPreview/StationCarePreviewUpgrade.cs",
    "unity/HomelessPetHelper/Assets/Scripts/Editor/CityMap/ForestEdgeArtImport.cs",
    "unity/HomelessPetHelper/Assets/Scripts/Editor/Field/FieldPropArtImport.cs",
    "unity/HomelessPetHelper/Assets/Scripts/Editor/Loading/LoadingArtworkUpgrade.cs",
    "unity/HomelessPetHelper/Assets/Scripts/Editor/StationHabitat/StationCatAssets.cs",
    "unity/HomelessPetHelper/Assets/Scripts/Editor/LevelUi/UiLayoutMigration.cs",
    "unity/HomelessPetHelper/Assets/Scripts/Editor/LevelUi/UiLayoutLegacyMigration.cs",
    "unity/HomelessPetHelper/Assets/Scripts/Editor/LevelUi/UiLayoutMapMigration.cs",
    "tools/unity.ps1",
    "unity/HomelessPetHelper/Assets/Tests/EditMode/StationAssetTests.cs",
    "unity/HomelessPetHelper/Assets/Tests/EditMode/ForestEdgeArtworkTests.cs",
    "unity/HomelessPetHelper/Assets/Tests/EditMode/TexturePlatformInspectorTests.cs",
    "unity/HomelessPetHelper/Assets/Tests/EditMode/CityMapLayerAssetTests.cs",
    "unity/HomelessPetHelper/Assets/Tests/EditMode/ImageResourceAuditTests.cs",
    "unity/HomelessPetHelper/Assets/Tests/PlayMode/ForestEdgeIntegrationTests.cs",
    "unity/HomelessPetHelper/Assets/Tests/StationHabitat/PlayMode/StationAppearanceTests.cs",
    "unity/HomelessPetHelper/Assets/Tests/StationHabitat/PlayMode/StationCatExpressionTests.cs",
    "unity/HomelessPetHelper/Assets/Tests/PlayMode/StationCareVisualPlayTests.cs",
    "tools/station-care-art.mjs",
    "tools/station-care-art-process.mjs",
    "tools/station-care-visuals.mjs",
    "tools/city-map-art.mjs",
    "tools/layout-b-art.mjs",
    "tools/image-size-preview.mjs",
    "src/duel/DuelApp.jsx",
    "src/layout-b/LayoutBApp.jsx",
    "tests/image-size-preview.test.mjs",
    "art-source/resource-catalog.json",
    "art-source/forest-edge-20261121/catalog-before.json",
    "art-source/city-map-neighborhood/workflow.json",
    "art-source/city-map-neighborhood/simplified-workflow.json",
    "art-source/rescue-layout-b/workflow.json",
    "art-source/station-care-temporary/README.md",
    "docs/B版单手操作布局基线.md",
    "docs/Main正式流程与资源迁移.md",
    "docs/城市地图与求助事件实现方案.md",
    "docs/救助站模块实现记录.md",
    "docs/林缘美术Unity接入.md",
    "docs/动物形象配置与制作流程.md",
    "docs/救助站同骨架坐下接入.md",
    "docs/conventions/PROJECT_BASELINE.md",
    "docs/prototypes/field-ui-20261121/style.css",
    "unity/HomelessPetHelper/Assets/Scripts/Editor/Field/FieldAuthorUi.cs",
    "unity/HomelessPetHelper/Assets/Scripts/Editor/StationPreview/StationPreviewAssets.cs",
    "art-source/README.md",
    "art-source/city-map-layered/README.md",
    "art-source/city-map-neighborhood/README.md",
    "art-source/rescue-layout-b/README.md",
    "art-source/forest-edge-20261121/README.md",
    "art-source/level-design-20261122/README.md",
    "art-source/loading-comfy-20261123/README.md"
  ]
}
```

## 不负责与依赖

- 不触碰并行会话已完成任务的产物：`CityMapConfig.json`、`CityMapText.json`、`StationPreviewText.json`、`LevelUiText.json`、`WorldTime*`、`MainGameRoot.cs`、`LevelOverlaySession.cs`、`FieldSamplePreview.cs`、`MainMusicTests.cs`、4 个未跟踪协作任务文档、`tools/wechat.ps1` 的本地微信改动。开工前已用 `collaboration.mjs read` 核对：全部并行任务状态为「完成」，唯一未结束 `20261120-协作同步-01a1f74d`（待协调）仅占 `docs/当前协作状态.md`，本任务不写该页。
- **ValleyIcons 12 张全部保留**（用户最终决定）。`Panel_CityMap.prefab` 保持不动（其 `_detailArt` 活绑定 7 张 ValleyIcons）；`CityMapLayerAssetTests` 中「Main 不含 ValleyIcons」的过时断言已按当前事实修正，不删 ValleyIcons、不改 Prefab。
- 不改存档 schema / 版本号，不新增美术资源，不改 UI 排版与视觉规范，不改玩法规则。
- `WX-WASM-SDK-V2/Editor/`（微信第三方 SDK）、`test-results/`、`docs/协作执行记录/` 历史 JSON 明确排除。
- 图片台账按 `art-source/README.md` 同步，`npm run assets:check` 验证。

## 统计、细案与实际落地

### 根因与统计（只读探查已完成）

- 用户已删 13 组 / 26 文件（图片 + .meta 成对，无孤立 .meta）：`Art/CityMap` 8 张、`EditorScenes/RescueCaseArt` 1 张、`EditorScenes/StationArt` 4 个。
- **2 处 Prefab 悬空 GUID**：`Panel_RescueCase.prefab:1837`（`b79ffb…` 已删 ShelterTemporary）、`Panel_StationWhiteboard.prefab:6909`（`c17810…` 已删 T_Station_cat）。其余 11 个已删 GUID 无任何序列化引用。
- **预制体**：18 个；9 处 `Legacy*` 节点仍被运行时代码引用（`_legacyContent`/`_legacyCat`/`LegacyViewport`），不是死节点，不动。
- **文档**：282 个 md；6 个正文文档各 1 行引用已删资源名；`Assets/Art/CityMap/README-临时图说明.md`、`Assets/Art/Station/README.md` 明显过时。
- **配置**：`CityMapLayers.json` 7 条 + `UiAtlasGroups.json` 4 条指向已删 `T_CityPlace_*`。
- **Editor**：100 个脚本；「和当前流程不一致的早期脚本」10 个（7 个一次性迁移/导入 + 3 个 `UiLayoutMigration` 闭环），90 个必留（构建钩子、Inspector、9 个预览宿主、测试反射依赖、共享 helper）。测试 using / asmdef / 反射 0 处需改。
- **测试**：5 个文件 8 条断言引用已删资源名；另有针对 4 个 Temporary 资源的 6+ 处断言。
- **跨工程**：`tools/` 6 个、`src/` 2 个、`tests/` 1 个引用旧资源名；`art-source` 台账/工作流 9 个。
- 工程无 `[Obsolete]`、`#if false`、TODO/HACK、注释掉的代码块；`Legacy*` 是运行时语义，不是过时标记。

### 细案

- **A. 动物形象统一 + 清理 Temporary**：
  - 正式形象（已存在、已导入 Sprite）：猫 `Art/PetMotion/T_Pet_original.png`（`e8ab41f882845da4e8d1a7be9a64343a`）、狗 `Art/Outing/T_Outing_DogNatural.png`（`cd137a207a0a70f47bed528797be805e`）。
  - `GameContent.asset:20-21` 的 `_cat/_dog` 改绑正式形象；`Panel_StationLoop.prefab:8273,8274,17238` 与 `Panel_RescueCase.prefab:1477` 的静态占位图同步改绑。
  - 4 个 `T_Station_*Temporary*` 资源：**删 2 张 PNG + .meta**；**2 个 128×128 Mask.bytes 与 CareProjection.bytes 保留**（护理网格与存档契约），但去掉"Temporary"命名歧义——Unity `.meta` 存 GUID，仅重命名文件+meta、GUID 不变，所有 GUID 绑定与存档字节不变。
  - `StationArtwork.Get()` 保持 128×128 强校验（护理玩法需要），`MainResidentCodec.ValidateMask` 保持逐字节（不改存档）；用户已授权开发期不介意存档，但保留契约更安全，只把资源名去掉 Temporary。
  - `DirtSurfaceView.BindArt` 的 `_legacyCat/_legacyDog` 身份键改指向正式形象（与 `_rescueCat/_rescueDog` 同 GUID），`CareCoatProjection` 继续生效。
  - 同步改 `MainSceneAuthor.cs:46-52,81-84`、3 个待删 Editor 工具的引用（随工具删除消失）、`tools/*.mjs`、`src/*.jsx`、6 处测试、台账。
- **B. 现场背景兜底**：`GameContent.GetOutingArtwork` 查不到 pointId 时回退到第一条（parking）；`ValidateArtwork` 去掉逐点 `GetOutingBackground` 强校验；`GameContent.asset._outingArtwork` 缩到 parking + treehouse 2 条；删其余 5 张 `T_Outing_*.jpg` + .meta；`Panel_Approach`/`Panel_SiegeTrial` 静态 LocationBackground 的 yard GUID 改指 parking；改 `ForestEdgeArtworkTests` / `ForestEdgeIntegrationTests` / `FieldPetMotionPlayTests` / `FieldAnimalDataPlayTests` 断言为「兜底一致」。
- **C. 早期 Editor 清理**：删 10 个 .cs + 10 个 .cs.meta；摘 `tools/unity.ps1` 的 7 个 ValidateSet 枚举项与 6 个 switch case（含两处 manifest 复制）。保留 90 个脚本；`check-unity.mjs` 预期 scripts 526→516、assemblies 37、issues 0。
- **D. 预制体/空目录/死配置**：2 处悬空 GUID 置 `{fileID: 0}`；删 2 个空 `EditorScenes` 目录 + .meta；`CityMapLayers.json` 与 `UiAtlasGroups.json` 清掉已删路径（或整条移除 group 内失效项）；修 5 个测试文件 8 条断言；改跨工程 `tools/`/`src/`/`tests/` 引用。
- **E. 文档**：6 个正文文档就地改成当前事实；重写 2 个 Assets README；art-source 台账/历史快照保留但加注「历史快照，资源已删」；`resource-catalog.json` 同步删除条目以免 `assets:check` 失败。

### 实际落地（2026-11-26 02:36）

- **A/B/C/D/E 全部完成**：
  - 删 2 张 `T_Station_*Temporary.png`；2 个 mask 改名为 `T_Station_*CareMask.bytes`（GUID/字节不变，护理网格与存档契约保留）。`GameContent.asset` 的 `_cat/_dog` 与 5 个 Prefab 的静态 sprite 统一改绑 `T_Pet_original.png` / `T_Outing_DogNatural.png`。
  - 现场背景删 5 张 `T_Outing_*.jpg`，`_outingArtwork` 缩到 parking + treehouse；`GameContent.GetOutingArtwork` 加兜底、`ValidateArtwork` 放宽为「至少一条有效兜底」。
  - 删 10 个早期 Editor 脚本 + `.meta`（100→90），摘 `tools/unity.ps1` 的 7 个枚举与 6 个 case；`check-unity.mjs` 为 516 scripts / 37 assemblies，27 项存量格式告警无新增。
  - 删 `Assets/EditorScenes/StationArt/`、`RescueCaseArt/` 两个空目录及 `.meta`（GUID 经全库核对无外部引用）。
  - `CityMapLayers.json` 清空 layers；`UiAtlasGroups.json` 去掉 4 条失效路径并补 4 张现存 Storybook 地标图，保持 8 条有效纹理（`AtlasToolingTests` 10/10 通过）。
  - 修 6 个测试文件的失效断言/路径；`ForestEdgeArtworkTests` 重写为「2 条配置 + 其余兜底 + 未知点回退」；`CityMapLayerAssetTests` 删掉针对已删图的 7 个 TestCase；`StationAssetTests` 重写为「正式形象 + CareMask 契约」。
  - **ValleyIcons 断言修正**：该断言在 11-25 23:02 通过、11-26 00:53 起因并行会话把 ValleyIcons 绑入 `Panel_CityMap.prefab` 而失败，属既有失败；用户决定保留 ValleyIcons 后，本轮把「Main 不含 ValleyIcons」按当前事实修正（测试改名 `MainUsesStorybookTerrainAndNoRetiredPlaceSprites`，保留「旧 `T_CityPlace_*` 无残留」断言），不删资源、不改 Prefab。
  - 跨工程 `tools/*.mjs`、`src/*.jsx`、`tests/image-size-preview.test.mjs`、原型 CSS 全部改到现存资源；`art-source/resource-catalog.json` 删 18 条 `MissingFile` 登记（182→164），`npm run assets:check` 恢复 exit 0。
  - 文档：2 个 Assets README 重写为当前事实；7 个正文文档加统一清理注记；`PROJECT_BASELINE.md` 与 `林缘美术Unity接入.md` 就地改当前事实；`art-source/README.md` 集中加「历史快照加注」，7 个批次 README 加注；历史 workflow/history/JSON 快照原样保留。

## 验证与交付（2026-11-26 02:36，全部实际执行）

- `npm run assets:check`：exit 0，error 0（清理前 18 条 MissingFile）。
- `npm run test:assets`：13/13 通过；`node --test tests/image-size-preview.test.mjs`：5/5 通过；`npm run build`（Vite）：exit 0。
- `node tools/check-unity.mjs`：516 scripts / 37 assemblies / 27 项存量格式告警（与改动前一致，无新增）。
- 全库扫描：32 个已删资源 GUID 零残留，仅两个 mask GUID 按设计保留；运行代码/配置/测试无失效引用（剩余命中均为历史台账、协作记录、描述性注记与 1 处「断言旧图不存在」的正向检查）。
- Unity 模块回归：复用 `test-results/unity/isolated-EditMode-baa6e838f4cc492480de0f16687cbc8e` 完整 Library 副本，用 `robocopy /MIR` 同步当前 `Assets`/`Packages`/`ProjectSettings` 后运行 `Unity.exe -batchmode -runTests`（报告 `test-results/unity/wbclean-1126-*.xml`）。
  - 受影响 EditMode 6 类：**36/36 通过**（exit 0）。
  - 全量 EditMode：**901 项 / 893 通过 / 8 失败**。对比 11-26 01:07 基线 910/879/31，本轮修好 23 项资源相关失败；剩余 8 项全部是基线既有的 `RescueLoopPreviewTests` 7 项 + `FormalOpeningTests` 1 项，非本任务引入。
  - 受影响 PlayMode 7 类：**22 项 / 18 通过 / 4 失败**（两次运行结果一致）。其中 `FieldAnimalDataPlayTests` / `FieldPetMotionPlayTests` 的 `IsRescueComplete` 2 项在 11-25 22:59 `repro-PM.xml` 基线即失败；`ForestEdgeIntegrationTests` 2 项由上一轮地图链路修复（返回链路 / 点击必进现场）改变行为所致，非本轮资源清理引入。本轮不越界改玩法流程测试。
- 未验边界：微信/AOT/真机、人工试玩、完整玩法回归、图片预算与许可、构建与渠道；本轮为开发期模块验证，不称全门禁通过。
- 未提交推送：全部改动保留在工作树，提交/推送由用户决定。

## 占用与交接

本任务已完成，上述 writePaths 全部释放（未提交推送，工作树改动保留供用户复核）。并发会话产物与用户本地微信改动全程未触碰。
