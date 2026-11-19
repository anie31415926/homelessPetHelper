# Unity 正式工程底座

日期：2026-11-17。阶段：工程底座、独立选项管理及白板验证、救助站基础规则，不是玩法移植完成或可发布版本。

## 当前范围

- 工程：`unity/HomelessPetHelper`，Unity **2022.3.62f3 LTS**。
- 轻量 2D 内置渲染管线，无 URP、3D、联网或第三方运行时框架。
- 工程包中现有 MCP for Unity 编辑器辅助工具，版本见项目基线；本次选项实现未安装或调用，核心规则不依赖它。包存在不等于当前助手已连接编辑器。
- 单主场景 `Assets/Scenes/Main.unity`，仅有正交相机与 `GameBootstrap`。
- 锁定竖屏；540×960 为桌面预览尺寸，不替代后续 UI 的 1080×1920 设计基准和安全区适配。
- 用户最新允许无图片白板 UI：静态 Prefab 和独立 Editor 验证场景已完成，Main 不变；没有运行时拼装面板，也没有正式图片、字体或原型美术导入。
- 原有 HTML 原型、测试和存档完全独立，不迁移、不覆盖。

## 已实现的边界

`RescueFlow` 是可脱离 Unity 场景测试的普通 C# 对象，唯一持有流程阶段：

```text
Station -> Approach -> Negotiation -> Rescued -> Care -> Station
```

它只校验阶段顺序、重复完成信号及暂停，不判断路径可解、交涉成功、照护完成，也不发奖或存档。
后续玩法模块先完成自身规则校验，再提交完成信号；不能把 UI 点按直接当作成功凭证。
当前尚无异步操作，不承诺跨会话回调防重；引入延迟或异步模块时须补会话标识及过期结果验证。

`GameBootstrap` 是装配入口，创建本次内存会话，并合并失焦与切后台状态；两者均恢复才允许流程继续。
没有静态单例、全局事件总线、DI 容器、Update 扫描、资源加载或常驻跨场景对象。
重新加载主场景会创建新会话；尚未实现退出恢复或存档。

独立选项管理 `Modules/Rescue/Options` 已实现：数量参数、选项实例身份、跨回合锁定、使用后消耗、回合供给/刷新、会话/回合校验、暂停与关闭。输入为选项 ID 集合，当前只允许单选；具体多选结算未启用。
该容器尚未接入 `GameBootstrap`，不执行动物效果、随机抽取或真实安全评估，未修改主场景。接口契约、验证与恢复入口见[选项管理模块实现记录](选项管理模块实现记录.md)。

2026-11-17 新增 ST-01：`Modules/Station` 持有临时入站名册、不可变回站总结和容量；`Modules/WorldTime` 提供显式推进、带会话/版本校验的整数游戏时钟。均为无 UnityEngine 依赖的独立 C# 程序集，未接主场景、未新增 UI 或自动健康结算。范围、调用契约、暂定表示及证据见[救助站模块实现记录](救助站模块实现记录.md)。

## 结构与依赖

```text
Assets/
  Scenes/Main.unity
  Scripts/Bootstrap/       -> Modules.Rescue
  Scripts/Modules/Rescue/  -> 无 UnityEngine 依赖
  Scripts/Modules/Station/ -> 无 UnityEngine 依赖，独立入站/容量规则
  Scripts/Modules/WorldTime/ -> 无 UnityEngine 依赖，显式行动世界时钟
  Scripts/Editor/          -> Bootstrap，仅 Editor
  Tests/EditMode/          -> Modules.Rescue，仅测试
  Tests/PlayMode/          -> Bootstrap、Modules.Rescue，仅测试
```

每一层有独立 asmdef，Editor 与测试不进入普通 Player 构建。命名空间对应实际目录。
Framework、Config、Network、Art、Prefabs 等在确有实现或资源时再建立，不提前堆空模块。
`Bootstrap` 为项目专用装配目录，不承担共享基础设施或 UI 业务。

本轮未引入跨模块通用模式基础设施或第三方架构框架。流程采用普通枚举和显式分支，理由是当前仅有五个顺序阶段，尚不需要状态类体系。
状态由 `RescueFlow` 持有；创建与生命周期由场景入口持有；迁移失败不修改状态。测试覆盖合法、非法、重复、暂停和重新进入。
这是一项局部组织方式，非全项目架构框架选型；人工复核仍待用户/指定 Reviewer 完成。

## 打开与验证

轻量图片资源检查已提供菜单和隔离命令：`Homeless Pet Helper > Tools > Check Image Resources` 或 `powershell.exe -NoProfile -File tools/unity.ps1 -Task ResourceAudit -Isolated -ReportSuffix MyRun`。按需只读扫描，不改导入设置或绑定、不自动打图集；范围、报告解释及最新验证见[工具记录](轻量资源检查工具.md)。

Unity Hub 添加 `unity/HomelessPetHelper`，使用同版本编辑器打开 Main 场景即可。
Play 后暂时只显示纯色背景；后续图片 UI 和场景表现会逐模块加入。

体验已完成选项功能：菜单 `Homeless Pet Helper > Preview > Open Option Whiteboard` 打开排除打包的白板场景再 Play。选择、保留、使用换轮、暂停与重开可操作；资源判断、可换图入口及样例/生产边界见[白板记录](白板UI与资源管理.md)。

2026-11-19 新增共享闭环：`Homeless Pet Helper > Preview > Open Rescue Loop` 后 Play，空站外出、地图单车棚、真实救助、同一个体入站、检查/可选护理、再次外出。仅为独立 Editor 验证，Main 未接入，详情及当前边界见[闭环接入记录](救助闭环与动物数据接入.md)。原独立入口继续保留，后续编辑已保存 Prefab，不重复运行创建工具。

2026-11-19 用户另行授权逆塔防 HTML 迁移：新增 `Homeless Pet Helper > Preview > Open Reverse Defense Trial` 后 Play。三路出兵/场上合体/双条暂停及守方加强已迁到独立纯C#规则和静态Prefab；不替代上述闭环、停车棚或Main。唯一配置源、试玩操作、测试/开发包和限制见[Unity逆塔防记录](逆塔防Unity试做记录.md)。`tools/unity.ps1` 新增隔离 `SiegeAssets`（仅首次创建、拒绝覆盖）、`SiegeLayout`（本轮已有Prefab局部编辑）和 `SiegeBuild`（只构建独立试玩）；常规迭代不重复运行创作工具。

2026-11-19 动物动作样例接入：菜单 `Homeless Pet Helper > Preview > Open Animal Motion` 后 Play。原图/六动作/暂停/调速/进度/复位使用独立静态 Prefab 与原生蒙皮骨骼动画，不是序列帧，不改 Main 或闭环。8 张临时图逐字节沿用 HTML，当前仅直接切姿态，没有站坐卧过渡；大小、独立验证与制作命令见[接入与资源评估](动物动作Unity接入与资源评估.md)。不重复创建已有面板。

仓库根目录执行，需 PowerShell 7、Node.js 与已授权的 Unity 编辑器。先关闭该工程的其他编辑器实例。

若工程正在编辑器中打开，不必关闭用户应用：给以下命令加 `-Isolated`，在 `test-results/unity/` 的独立副本执行，避免同一工程双开。仅复制已保存的 Assets/Packages/ProjectSettings 和包缓存，不验证未保存的场景编辑；每次副本保留以便复核，不修改源工程设置。报告为 `<Task>-isolated.log/xml`，构建产物位于对应副本的 Builds；该目录已忽略，不入库。

```powershell
pwsh -NoProfile -File tools/unity.ps1 -Task Initialize
node tools/check-unity.mjs
pwsh -NoProfile -File tools/unity.ps1 -Task EditMode
pwsh -NoProfile -File tools/unity.ps1 -Task PlayMode
pwsh -NoProfile -File tools/unity.ps1 -Task BuildDevelopment
pwsh -NoProfile -File tools/unity.ps1 -Task Build
```

例如：`pwsh -NoProfile -File tools/unity.ps1 -Task EditMode -Isolated`。隔离模式不是另一套代码来源，修改仍只在正式工程进行；构建前重新复制当前已保存源码。

脚本按 ProjectVersion 查找默认 Hub 安装路径，也可传 `-EditorPath` 或设置 `UNITY_EDITOR_PATH`。
Initialize 设置工程基线，只在 Main 尚不存在时创建场景，不覆盖已有场景内容；不是面板生成器。
测试命令会检查退出码和 XML 结果，拒绝空报告，并恢复 Test Framework 临时改动的 PlayerSettings。
构建产物位于工程 `Builds/`，日志和 XML 位于根目录 `test-results/unity/`，均不入库。

## 检查与责任

当前工具版本：Unity 2022.3.62f3、Test Framework 1.1.33、Node.js 24.14.1、PowerShell 7.6.9。
`.editorconfig` 约束 C# 四空格、Allman、UTF-8 无 BOM 和 LF；Unity 编译器检查语法与类型。
`check-unity.mjs v1` 仅检查基础格式、命名空间、文件类型名、meta/GUID、Resources 目录与本地程序集依赖，无额外包依赖。
它不是完整 C# 格式器、复杂度分析器或 Roslyn 安全分析器，不能声称已覆盖这些检查。白板接入后明确支持工程已安装包 `com.unity.ugui` 提供的 `UnityEngine.UI` 程序集引用，其余未知引用仍报错。

| 门禁 | 当前执行 / 失败判定 | 负责人及后续 |
| --- | --- | --- |
| 源码基础检查 | `node tools/check-unity.mjs`；issues 非空失败；报告 `source-check.json` | 本轮 Codex 执行；后续提交者持续执行 |
| 编译与规则回归 | Initialize、EditMode、PlayMode；非零退出、缺报告或测试失败阻断 | 本轮 Codex 执行；用户/指定 Reviewer 复核 |
| 桌面构建 | Build / BuildDevelopment；BuildReport 非成功阻断，不以开发包代替正式包 | 本轮 Codex 执行；安装文件缺失须先修复环境 |
| 人工 Review | 本轮实现待用户/指定 Reviewer 复核；不把自动化通过当作人工批准 | 合入前指定 Reviewer |
| 完整静态分析与 CI | 尚未建立，不声称通过；当前靠基础脚本、编译器及人工检查 | 工具负责人待用户指定；首个正式玩法模块合入前评审，计划日期待确认 |
| 真机与微信渠道 | 未验证，不允许据此发布 | 设备/导出方案待用户确认；首次渠道包前执行 |

资源、UI、SDK 与更新门禁本轮不适用；这些实现尚未开始，不能据此认为将来免检。
尚无二进制正式资产，Git LFS、文件锁及 Owner 策略须在第一批图片/Prefab/图集入库前确认；届时禁止并行修改同一资产。

## 工程底座初始验证结果

- Initialize：成功，Unity 编译未报告 C# 错误或警告。
- 源码基础检查：9 个脚本、5 个程序集，无报告问题；meta/GUID 和依赖方向检查通过。
- EditMode：13/13，通过流程顺序、全部拒绝分支、未知输入、重复完成、暂停与实例隔离测试。
- PlayMode：3/3，通过主场景启动、焦点/后台组合恢复与场景重载测试。后台事件为测试中模拟，不是真机打断验证。
- Windows 64 位 Mono 开发构建：成功；产物仅含两个业务程序集，不含项目 Editor/测试程序集。没有进行正式 UI 或画面验收。
- Windows 正式构建：失败，已确认是安装中缺少非开发版 Player 文件，非 C# 编译失败，详见下节。
- 未运行 Web 原型回归：本轮未修改其源码与依赖；也未验证微信、真机性能或 IL2CPP/AOT。

## 选项管理新增验证

- `node tools/check-unity.mjs`：19 个脚本、5 个程序集，issues 为空。
- 隔离副本 EditMode：77/77 通过，其中原有流程 13、新增选项规则 64。
- 隔离副本 PlayMode：原有 3/3 通过，证明底座回归，不代表选项已接入场景。
- 含工程现有 MCP 编辑器包的最新隔离副本 Windows 64 位 Mono 开发构建成功，84,254,398 bytes；三份运行日志无 `error CS` / `warning CS`。启动授权 token 更新提示未阻止本地授权测试/构建，详见模块记录。
- 命令、报告、实现边界和未验证项详见[模块记录](选项管理模块实现记录.md#实际验证结果)。不重跑已知安装缺项的非开发 Build，不声称微信/真机、覆盖率、完整分析器或人工 Review 已完成。

## 本机已知限制

救助站新增试玩：菜单 `Homeless Pet Helper > Preview > Open Station Whiteboard` 后 Play。总结仅展示结果，进入站内再选动物、查看样例健康与擦洗；静态 Prefab 和 Editor 样例会话独立于 Main。点击、长按或拖动擦除污渍，达到完成条件后按“完成护理”才推进一次游戏时间。边界、暂定参数、实际测试和证据统一见[救助站模块实现记录](救助站模块实现记录.md#st-02--st-03站内白板与局部清洁)，不要用下文 ST-01 历史数量代表本轮结果。

ST-01 最新验证：41 脚本/8 程序集基础检查无问题，EditMode 131/131（新增 51 项）、既有 PlayMode 6/6 回归通过，桌面开发构建 84,334,747 bytes。证据已归档 `test-results/unity/station-ST01/`；这不代表救助站已接入场景或正式 UI 可试玩，后续从[模块记录](救助站模块实现记录.md)恢复，不沿用下述旧测试总数。

白板阶段新增结果：28 脚本/6 程序集基础检查无问题，EditMode 80/80、PlayMode 6/6，含 6 个屏幕比例的离屏渲染与非空像素检查；最新桌面开发包 84,286,506 bytes。Main 与构建场景列表未变，产物无项目 Editor/测试程序集。入口、截图和限制以[白板验证记录](白板UI与资源管理.md#验证与交付)为准，不把旧 77/77、3/3 数量当当前总数。

- 批处理环境缺少标准 Windows 变量时 UPM 会报 `path ... undefined`；脚本仅对子进程补齐 `ALLUSERSPROFILE` 和 `PUBLIC`，不修改系统环境。
- 首次导入曾遇到 UPM 重命名缓存的 EPERM。本机通过从编辑器原始内置包及同版本官方全局缓存复制到忽略的 Library 缓存后导入成功；没有改包源码、嵌入依赖或修改系统权限。全新缓存的自动导入仍需另机验证。
- 本机仅安装 Windows Standalone 支持，且缺少 64 位非开发版 `WindowsPlayer.exe`。正式 Build 曾在后处理复制阶段失败；需要通过 Hub 修复对应支持文件。构建脚本现提前报告缺失，不偷偷降级成开发包。
- 微信小游戏转换工具、SDK、WebGL/IL2CPP 工具链及真机测试均未接入。编辑器测试或桌面开发包不等于微信小游戏可发布。

## 下一步顺序

后续执行以[《开发方案与执行步骤》](开发方案与执行步骤.md)为准，避免两处维护不同的任务状态。

先做微信最小技术验证，再推进回合救助、开路规则与会话串联。平台条件不足时可继续独立规则开发，不把平台未验证视为已通过。
无图片选项白板已可体验；正式图片 UI 与互动在方案确认后单独制作。存档、领养、奖励和经营逐项确定，不从白板样例推断尚未确认的玩法。
