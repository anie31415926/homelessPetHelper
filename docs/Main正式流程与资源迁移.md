# Main 正式流程与资源迁移

> 2026-11-26 清理注记：本文提到的 `T_CityPlace_*.png`、`T_Station_*Temporary*`、`T_Rescue_ShelterTemporary.jpg`、`T_CityMap_Forest.jpg` 等临时/过时资源，以及 `RuntimeMove`、`ForestEdgeAssets`、`StationCareLayout`、`StationCatAssets` 等一次性 Editor 工具与命令，均已在本轮清理中删除；以下相关段落保留为当时事实，当前资源与工具以工程现状为准。

2026-11-20，用户授权将可用 Editor 资源迁入运行时目录，明确以逆塔防替换正式流程中的回合救助；回合方案保留为备选。本页记录运行入口的技术接通，不代表最终美术、完整经营或渠道发布验收。

## 试玩入口

Unity 2022.3.62f3，菜单 `Homeless Pet Helper > Open Main Game`，打开保存的 `Assets/Scenes/Main.unity` 后 Play。Main 仍是唯一 enabled 构建场景。

1. 空救助站点击“外出救助”，进入原地图。
2. 单车棚普通求助点击“前往这里”，进入真实逆塔防；正式接入模式不提供重抽种子、换起点或同局重置。
3. 实际战斗 Won 后点击“安全转运回站”，才将本局同一只动物入站；超时或主动结束可回站，但不会生成已救助动物。
4. 回站总结只展示成果、动物和待检查情况。点击“进入救助站”后，在站内选择动物查看健康、清洗或梳理。
5. 当前明确健康样猫完成检查即可再次外出，不强迫清洗。有效清洁成果留在同一个体，退出护理再进入不会重新散布污渍。

旧回合入口 `Homeless Pet Helper > Preview > Open Rescue Loop`、独立逆塔防和动作预览均保留。它们是分别创建的试做会话，不与 Main 共享存档，旧回合没有混入正式现场选择。

## 装配与状态

| 位置 | 本轮职责 |
| --- | --- |
| `Assets/Scripts/Game/` | 正式 composition 程序集，迁出的地图/文案/站内 presenter 不再依赖 UnityEditor |
| `MainGameRoot` / `MainLoopPresenter` | 显式序列化绑定三个保存面板、GameContent 和 Bootstrap；创建并释放唯一 GameLoopSession |
| `SiegeRescueActivity` | 实现原 IRescueActivity，用真实 SiegeSession 胜负桥接；同一个 AnimalIndividual 进入站内，不复制成功样本 |
| `SiegeTrialView.BindEncounter` | 正式模式借用外部单局，独立预览仍自己管理单局；回站拒绝时保留现场并显示原因，解除阻塞后可重试 |
| `GameBootstrap.BindSession` | 失焦/切后台暂停同一个流程，恢复焦点不擅自继续；须在当前面板明确恢复 |
| `Assets/Settings/Game/GameContent.asset` | 显式引用随包配置、文案、护理 Sprite/128² Mask 和字体，无 Resources 查找或运行时 AssetDatabase |

入站预检、容量、请求身份、历史总结及一次时间结算继续由原业务模块负责。本次样例返回仍结算 25 游戏分钟，不把真实战斗秒数直接当世界时间，不伪造清路成果。已胜利后暂停也不能绕过暂停状态转运。

样本限定为单车棚 `cat.shy`：初始健康明确无病伤、脏污65、浮毛50、护理许可显式开放。胜利只更新接受接近的情绪/行为，不把其他动物的未知健康强行改为健康。尚未增加健康随机、诊断、伤口小游戏或送医。

## 资源位置

迁移在隔离 Unity 中用 AssetDatabase.MoveAsset 执行，按49项清单同步原工程，保留 `.meta`/GUID；不是删除后重新导入。清单为 `test-results/unity/isolated-RuntimeMove-94bd6fc9647346d8aa5b616629b7cbc1/RuntimeMigrationMoves.json`。

| 原目录 | 现目录 |
| --- | --- |
| `Assets/EditorScenes/CityMapArt/` | `Assets/Art/CityMap/`，地图地面、地点和动物角标 |
| `Assets/EditorScenes/StationCareArt/` | `Assets/Art/Station/`，两张护理图及对应遮罩 |
| `Assets/EditorScenes/PetMotion/` 中图/材质/mesh/clip/Shader/Widget | `Assets/Art/PetMotion/`；预览场景、文本、测试夹具仍留原目录 |
| CityMapConfig、CityMapText、StationPreviewText | `Assets/Settings/Game/` |
| SiegeTrialData、SiegeTrialText | `Assets/Settings/Siege/`，源表仍为 src/siege，修改源后运行生成器 |

旧 StationArt、RescueCaseArt、回合专属配置与预览仍留 EditorScenes；本轮已验 Main 递归依赖不含 EditorScenes。迁移相关离线工具及旧 HTML 图片 import 同步新路径，不复制第二份运行图。

Main 的逆塔防动物位使用已有优化 rig，经透明 RenderTexture 显示；rig 放场景根节点避免 Canvas 坐标破坏。只有战斗运行时播放，离开面板停用。护理仍使用与 Mask 配对的静态图，没有把脏污直接贴到会变形的动画上。六动作和跨姿态过渡的边界见[动作记录](动物动作Unity接入与资源评估.md)。

新增 `Assets/Art/Fonts/NotoSansSC-Game.ttf`，Noto Sans SC / OFL1.1、weight400 子集，133412 bytes /546 cmap entries；来源哈希、生成命令与许可在同目录 README/OFL。不是系统微软字体，不保证任意新中文输入均有字形；加文案后须重新生成子集。构建将许可复制到 Player 旁。

### 大小口径

MainFinal移交前：实际静态图片依赖12项，源文件合计 **1067.9 KiB**（动作图集1、地图9、护理2，包括当前预绑定的狗图）。按 first-play 如实登记，不能因面板隐藏就当按需卸载。另21张仅试做/历史图为1313.1KiB；全库仍保留原图。

动作图集仍为876×924 /518906bytes，基础RGBA32约3.09MiB，clip/mesh共享，不按动物复制；详细优化前后数据沿用[同口径记录](动物动作Unity接入与资源评估.md)。这些都不是微信下载、GPU显存或整包结果。本轮未增加 Addressables/CDN/远程下载器，资源全部 temporary，未批准正式美术/来源许可/微信交付；台账draft和审核提醒保留。

## 本轮验证

验证对象为已保存源码的隔离副本，未关闭用户 Editor、未覆盖未保存场景。没有统一源码指纹机制，因此报告只适用于下列快照；之后图集任务的改动需另验。

| 检查 | 结果与证据 |
| --- | --- |
| 全量图形 EditMode | **522/522**，`test-results/unity/EditMode-isolated-MainFinal.xml/log`；快照 `isolated-EditMode-c2134ccd3e6248dea04f4b062d2252cf` |
| 全量图形 PlayMode | **40/40，无跳过**，`PlayMode-isolated-MainFinal.xml/log`；快照 `isolated-PlayMode-cb8c6f8778a54b9ab668d24a107ad8fe` |
| 正式闭环 | 保存控件驱动真实逆塔防胜利、同一个体入站/25分钟、总结后站内检查、部分清洁/再次外出；超时/主动返回无误入站；转运临时拒绝后重试不重复计时 |
| 暂停与资源 | 失焦、后台、焦点先恢复但应用仍暂停、胜利后暂停；Main无EditorScenes依赖、运行时无Editor程序集引用、字体字形、动物RT真实颜色像素 |
| 画面 | MainEvidence 内390×844、540×960、768×1024各7状态，共21图；检查布局/文本，已查看地图、现场、总结和护理代表图，不等于真机触摸/安全区验收 |
| Node及生成 | 基础检查224脚本/23程序集、0issues；Siege生成3文件一致；HTML规则25/25、Duel15/15、公共图集7/7、台账工具13/13；Vite构建通过，仅既有Three大chunk提示 |
| 图片台账 | `npm run assets:check` 无缺失/漏登记/错误；保留首批草稿、坐姿超计划及12项未批准交付提醒 |
| Windows Development | BuildPipeline报告成功、94053291bytes，`BuildDevelopment-isolated-MainFinal.log`；快照 `isolated-BuildDevelopment-2f7eff17acd9424ca57bbd98e702035b`。**输出缺Mono运行库，不是可启动验收** |
| Windows原生启动 | 前一开发包启动报 `Failed to load mono`，日志 `test-results/unity/MainPlayer.log`。本机win64 development支持目录及最终输出均缺 `MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll`；测试错误窗口已关闭 |
| Windows Release | `Build-isolated-Main1.log` 失败，本机缺win64 nondevelopment Player。未自动修安装、改成32位或拷贝其他架构DLL |

最初几轮的地图JSON旧路径、旧A/B夹具引用、动画根节点/像素断言、帧驱动截图及新字体标题行高问题已修正，最终报告全过；保留失败日志，不删除测试或拿旧成功数代替本轮证据。原生包的安装问题没有被Editor测试掩盖。

空白检查排除Unity序列化Main/CityMap Prefab后通过（按进程参数兼容CRLF，未改仓库配置）。这两份保存资产的新增空字符串字段由Unity写成冒号后空格，保持生成结果；不宣称默认全库git diff --check通过。

复现主要命令：

```powershell
node tools/siege-unity-data.mjs --check
node tools/check-unity.mjs
./tools/unity.ps1 -Task EditMode -Isolated -Graphics -ReportSuffix MainCheck
./tools/unity.ps1 -Task PlayMode -Isolated -Graphics -ReportSuffix MainCheck
npm run assets:check
npm run test:siege
npm run build
```

不要重复跑旧白板创建工具覆盖绑定；Main/面板已保存，后续在Prefab Mode编辑。RuntimeMove/MainAssets是本轮隔离迁移/装配工具，不是每次Play的运行依赖。

## 接续边界

- 当前固定求助只够一次真实救助，没有自动补单；其他地点保持明确未接现场路径，不用单车棚猫冒充所有内容。
- 无磁盘存档，停止Play或重启Main会重建会话；不是长期经营可持久试玩版。
- 未新增医疗、领养、扩建、主动腾位、清路、寻宠交付、经济或通用动物生成。容量仍有限，不能无限收容。
- Windows原生试玩受安装缺件阻塞；微信转换、真机、触摸、性能及正式许可未验。没有自动提交或推送。
- 图集工具后续补齐已在[本任务协调记录](协作任务/20261120-Main迁移-runtime-main.md)移交；它可修改派生图集/生成链，必须自行重验，不能将MainFinal通过数套到修改后资产。

下一步先从Main人工试玩完整往返，收集核心玩法及站内操作反馈；Windows安装修复和微信验收单独推进，不据本轮迁移自动扩大玩法范围。
