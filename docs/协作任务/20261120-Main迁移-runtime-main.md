# Main 正式入口与运行时资源迁移

- 任务 ID：20261120-Main迁移-runtime-main。
- 负责人：当前用户会话的 Codex，工作标识 `runtime-main`；当前工具未返回会话 UUID，不猜填。
- 最近更新：2026-11-20T15:03:07+08:00。
- 状态：完成。
- 用户授权：将 Editor 下可用资源迁入运行时目录并接 Main；澄清后明确由逆塔防替换回合救助，旧回合作为备选。

## 写入范围

- `Assets/Scripts/Game/`：Main composition、逆塔防结果桥接、迁出的地图/文案/站内 presenter。
- `Assets/Scripts/Editor/{CityMap,OptionPreview,StationPreview,PetMotion,Siege}/`：预览适配引用与路径；相关 asmdef。
- `Assets/Scripts/Editor/{MainSceneAuthor,RuntimePreviewContent,RuntimeMigration,DesktopBuild}.cs`。
- `Assets/Scripts/Bootstrap/GameBootstrap.cs`、`Modules/Siege/UI/SiegeTrialView.cs`；不改 Siege 核心战斗规则。
- `Assets/Scenes/Main.unity`、已有 StationLoop/CityMap/SiegeTrial 面板和 SiegeChoice Prefab。
- 资源连同 `.meta`：EditorScenes/CityMapArt → Art/CityMap；StationCareArt → Art/Station；PetMotion 中图/材质/网格/动画/rig → Art/PetMotion；JSON → Settings/Game 与 Settings/Siege。预览场景/对照数据留 EditorScenes。
- `Assets/Art/Fonts/`、`tools/build-game-font.py`；开放许可字体子集。
- `Assets/Tests/` 对应路径/边界回归及 Main 新测试。
- `tools/unity.ps1`（RuntimeMove/MainAssets 与全量动作 A/B 夹具复制）、生图/派生/动画生成工具的迁移路径、`tools/resource-catalog.mjs`。
- `src/siege/unity-text.zh-CN.json` 只补正式回站文案，`tools/siege-unity-data.mjs` 生成到新路径。
- `src/layout-b/LayoutBApp.jsx`、`src/duel/DuelApp.jsx` 仅同步已迁图片的 import，不改旧 HTML 玩法。
- `art-source/resource-catalog.json`、相关资源记录/本任务模块记录/项目基线。

## 不负责与依赖

不接管同期兵种设计与工程保障。已读 `20261120-工程保障-01a1f74d`，对方仅完成协作文档；本任务不编辑其五份公共文档。保留其新建 AGENTS/风向标及其他未提交文档。`tools/unity.ps1` 在本任务结束前仍占用；`package.json` 已按下方协调移交图集任务，不再占用。

不新增清路、医疗、领养、刷新求助、经济、存档、跨姿态过渡或目标平台框架。不提交/推送。

## 当前事实与验证

- Main 通过静态绑定启动唯一 GameLoopSession；逆塔防实际 Won 后允许安全转运，超时/退出不新增动物；旧回合闭环预览保留。
- GUID 保留迁移已在隔离 Unity AssetDatabase 执行49项，再按清单同步源工程；MainAssets5已保存实际场景并无 EditorScenes 递归依赖。
- 全量图形 EditMode 最终522/522，PlayMode最终40/40，无跳过；报告 `EditMode-isolated-MainFinal.xml`、`PlayMode-isolated-MainFinal.xml`，快照分别 `isolated-EditMode-c2134ccd3e6248dea04f4b062d2252cf`、`isolated-PlayMode-cb8c6f8778a54b9ab668d24a107ad8fe`。
- 图集7/7、台账工具13/13通过。台账12个实际 Main静态依赖，1067.9KiB源图；临时/权限审核状态不自动升级。
- Windows Release构建因本机缺少win64 nondevelopment支持失败；Development最终BuildPipeline成功，但安装目录及输出均缺64位Mono运行库，原生启动报Failed to load mono。未改目标/未复制其他架构DLL/未自动安装，不能宣称原生试玩通过。
- Vite构建发现的旧HTML图引用遗留已修正，最终构建通过，仅有既有Three大chunk提示；Unity基础检查224脚本/23程序集、0issues，Siege生成一致性及HTML规则25/25已通过。
- 暂无统一验证指纹，测试仅对应各隔离快照；最终模块记录会列实际报告，不拿历史数覆盖未验改动。
- 最终模块记录已写入[Main正式流程与资源迁移](../Main正式流程与资源迁移.md)，闭环/逆塔防/动作/资源README及项目基线已局部同步。旧HTML回归Siege25/25、Duel15/15，Vite最终构建通过。
- 空白检查：排除Unity序列化的Main和CityMap Prefab后，兼容CRLF的git diff --check通过；这两份保存资产新增的m_Name/m_EditorClassIdentifier空字段有Unity生成的尾空格，原样保留，不宣称全库默认diff --check通过。基础源码检查0issues。
- 没有提交或推送；没有替其他任务改状态或覆盖其文件。

## 占用与交接

### 2026-11-20T14:59:52+08:00 图集任务协调

已收到用户转来的 `01a1ed54-d51a-76c1-80e3-a5cf24f9cd87` 协调消息，并读 `20261120-图集补齐-atlas-tooling`。以下共享路径**立即释放给该任务**；直接在现有未提交优化/迁移上局部续改，保留当前运行时路径与GUID，不回退到EditorScenes：

- `package.json`、`package-lock.json`。
- `tools/lib/`、`tools/pet-motion-atlas.mjs`、`tools/pet-motion-unity.mjs`；`tests/image-atlas.test.mjs`、`tests/pet-motion-assets.test.mjs`。
- `art-source/pet-motion/atlas.json`、`art-source/pet-motion/unity-export.json`、`art-source/resource-catalog.json`。
- `Assets/Scripts/Editor/PetMotion/`（含PetMotionAssets.cs、PetMotionBakeData.cs、Optimization及asmdef）；`Assets/Tests/PetMotion/`。
- `Assets/Art/PetMotion/` 及对应 `.meta`：允许必要派生资产、rig内部绑定更新，保留骨点/动作语义与外部引用GUID。

本任务的MainFinal验证和1067.9KiB是**移交前隔离快照**，后续图集改动须由图集任务自行重验生成链、动作与Main依赖，不能沿用本轮通过数。本任务不再写上述共享路径，也不重新覆盖其派生资产。

仍保留至本任务交付：`Assets/Scenes/Main.unity`；`Assets/Prefabs/Panels/Panel_{StationLoop,CityMap,SiegeTrial}.prefab`及`Widget_SiegeChoice.prefab`；`Assets/Scripts/Game/`、Bootstrap/GameBootstrap.cs、Siege/UI/SiegeTrialView.cs；`Assets/Scripts/Editor/{MainSceneAuthor,RuntimePreviewContent,RuntimeMigration,DesktopBuild}.cs`；`tools/unity.ps1`。图集任务已表示不改这些文件；如发现必须修改则再次协调。

当前仅收尾本任务独立文档（Main正式流程与资源迁移、救助闭环、逆塔防Unity记录、动物动作Unity记录、项目基线、art-source两个README、Art/CityMap说明）和验证记录；不改图集任务自己的文档。其新增Editor/Atlas、Settings/Atlas、Art/Atlases不在本任务占用内。

### 2026-11-20T15:03:07+08:00 交付释放

本任务Editor可玩入口、资源迁移及记录已完成。上述仍保留的Main/Prefab/运行时代码/创作工具/文档写入占用**全部释放**；后续任务先重读再认领，保留已存在未提交改动。`tools/unity.ps1`可供工程保障接续，`package.json`及动作派生链沿前述协调归图集补齐任务；释放不撤销其占用。

遗留：Windows安装缺win64 Mono及Release支持，原生试玩未通过；无存档/自动补单/医疗/领养，微信与正式美术未验。后续图集修改后由其任务重新验证Main与动作，不把本轮隔离结果当最新混合工作树的证明。无需本任务继续占用共享路径等待环境修复。
