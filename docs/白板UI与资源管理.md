# 白板 UI 与资源管理

日期：2026-11-17。状态：静态白板与交互已实现，编辑器验证通过，人工体验待反馈；不代表正式美术或完整救助玩法定案。

## 本轮范围

用户允许先做不带图片的白板 UI，体验已经完成的功能，并方便后续设置图片资源。此授权替代“所有 UI 暂缓”，但不解除玩法先讨论、静态 Prefab 与明确绑定的规范。

- 使用 Unity 已有 uGUI：静态面板 Prefab、可复用选项 Cell、标准 Image/Toggle/Button/Text；不安装 UI 框架。
- 先验证选择、锁定/解锁、使用后消耗、自动换轮、暂停及重新开始。样例供给用于看清选项流转，不实现未经确认的动物效果、随机分布、手册/求援结算。
- 验证场景放 `Assets/EditorScenes/`，样例驱动只在 Editor 程序集；不加入正式构建场景，不添加生产跳关/改状态入口。
- 展示组件与样例驱动分开；静态 Prefab 与序列化 Image 引用保留，换图不改变选项实例、选择和锁定逻辑。
- 设计基准 1080×1920，白/灰为主；选择和保留分别用 Toggle 状态表达；长内容可以滚动，操作区独立于列表。无插画、动效或美术批量生产。
- 中文文案置于随包文本资产，示例字体在编辑器内使用本机字体，不擅自复制操作系统字体进入仓库；正式可商用字体仍待选择。

## 微信小游戏是否需要资源管理

**结论：需要资源管理边界，但目前不安装完整资源框架，不自研下载/热更系统。**

微信小游戏并不等于不需要管理资源：首屏下载/启动、按需加载、解压后的贴图内存、同时在用的图集和资源释放，都可能比纯规则成本更高。具体配额和首包预算以当前官方规则、锁定 SDK 与实际导出测量为准，不照抄历史包体数字。

### 当前白板

没有动态寻址资源、远程内容或真正图片；验证场景直接引用静态 Prefab/字体占位/文本资产，属于编辑器验证装配，不伪装成已完成生产资源系统。不新增 Resources，不通过名字查找 UI，不创建万能 ResourceManager 或第二套缓存。

### 第一批正式资源前

按[资产规范](conventions/topics/assets.md#s-8-4)评审并接入统一资源寻址/加载入口，以本地模式起步；Addressables 与 AssetBundle 的具体方案须与微信转换 SDK 兼容性一起验证，当前未选型。加载入口负责加载/失败/实例归属及配对释放，UI 和动物规则不接触下载 API 或散落字符串路径。

资源管理不等于远程更新，也不等于必须购买 CDN。若实际首资源和包体允许，核心本地资源先随包；以后确需按需远程内容，再单独确认首次网络条件、域名、缓存版本和失败恢复。不要因为未来可能分包就现在接完整热更系统。

### 资源分组意向，待实测确认

| 分组 | 范围与生命周期 |
| --- | --- |
| CommonUI | 少量公共控件/字体/基础文案；控制依赖规模，不做全局巨图集 |
| Rescue | 本次救助场景、动物表现与工具；外出阶段持有，结束后按引用释放 |
| Station | 救助站与照护内容；站内按需持有，不与所有外出场景常驻 |

后续图片用独立 Sprite/图集，底板用九宫格，文案不烘焙进图片；卡面/按钮/背景均由 Prefab 中的 Image 明确引用。正式资源导入、许可、Git LFS/锁、资源 Owner、包体/启动/内存预算需在首批入库前确认，不因白板授权视为已完成。

## 恢复检查点

1. 已核对 UI、资产、生命周期及现有选项实现。当前无可用 Unity MCP 工具，使用仓库隔离验证命令，不关闭用户打开的工程。
2. 已实现 `Modules/Rescue/UI` 独立程序集：`OptionPanelView`、`OptionCardView` 与统一 `SafeAreaView`；仅依赖选项规则与已有 uGUI。列表 Cell 用 `UnityEngine.Pool.ObjectPool` 复用，面板不运行时造树；选中实例按集合保存，锁定刷新不丢选择，不以槽位绑定身份。
3. `Editor/OptionPreview` 保存样例装配、文本读取与创作工具；`OptionPreviewHost` 只在预览场景进入 Play 时创建普通 C# 会话，退出、换场景和脚本重载时关闭。静态事件只在编辑器域初始化一次，视图/会话订阅成对释放。没有 Editor-only MonoBehaviour 挂进场景造成 Missing Script。
4. 两份静态 Prefab 已保存到 `Assets/Prefabs/`；验证场景 `Assets/EditorScenes/SC_OptionWhiteboard.unity` 未加入 Build Settings，Main 未变。样例按固定序列供给六个候选名字，每轮马上模拟回应结束来刷新；所有样例行动被标为可稳定只是测试夹具，不是实际安全评估或动物回应。
5. 图片入口：卡片 `Artwork` Image 预先保存在 Prefab，未赋 Sprite 时隐藏；赋图后自动显示。卡片底板和按钮为 Sliced Image，可在 Prefab 中设置 Sprite/Border；控件标准 Transition 可改 SpriteSwap，不改变规则。不要通过重复运行创作工具覆盖后续美术。
6. 最终 PlayMode 6/6 通过（原有 3 + 新增 3）；截图发现布局组默认 flexibleHeight 撑高区域，已在 Prefab Mode 修复，并补按钮不异常拉高的断言。EditMode 80/80、六种比例渲染与开发构建通过，详见验证记录；没有微信/真机结果。
7. 正式动物回应/医疗/抽取规则继续先讨论。资源框架仍未选型，白板的 Editor 静态引用不代替生产统一资源系统；规范 §8.4 的生产接入放到第一批正式资源前，适用边界仅本验证场景，不扩为生产豁免。

## 如何体验与换图

1. Unity 2022.3.62f3 等待编译后，菜单 `Homeless Pet Helper > Preview > Open Option Whiteboard` 打开验证场景，再点击 Play；有未保存场景时菜单先提示保存，不强行切换。
2. 选一个行动、保留最多两个、使用后换轮；暂停与重新开始可直接验证。这里没有动物属性、医护或救助成功结算。
3. 后续在 Prefab Mode 编辑 `Widget_OptionCard` 的 Artwork/底板、`Panel_OptionWhiteboard` 的按钮 Image 与 Transition。图片与文字分开，替图不需要修改 `RescueOptionHand`；图片位置尺寸按布局参数调整，不拉伸整屏。
4. 预览字体来自本机，不会进正式包。正式字体、图集、图片许可及统一资源加载方案须另外评审；当前验证场景不提供 Player/微信包体验。

## 验证与交付

| 检查 | 实际结果与证据 |
| --- | --- |
| `node tools/check-unity.mjs` | 28 脚本、6 程序集，issues 为空；工具增加已安装 uGUI 程序集引用的明确映射，不忽略未知依赖 |
| `pwsh -NoProfile -File tools/unity.ps1 -Task EditMode -Isolated` | 80/80，包含已有 77 与新增 3 个资产检查；Prefab 无 Missing Script、绑定完整、SafeArea/CanvasScaler/换图入口存在、验证场景不在 Build Settings |
| `pwsh -NoProfile -File tools/unity.ps1 -Task PlayMode -Isolated -Graphics` | 6/6；真实静态 Prefab 操作、锁定后选择不丢、自动换轮、暂停/重开、六位置/重复刷新复用、关闭重开与单次监听验证通过；不等于真人触屏验收 |
| 同上渲染检查 | 540×960、390×844、768×1024、540×1080、540×1170、540×1200，画布像素非空；截图在 `test-results/whiteboard-screenshots/`。已查看长屏与 4:3 渲染，三项均可见、主操作未重叠；更多项可滚动 |
| `pwsh -NoProfile -File tools/unity.ps1 -Task BuildDevelopment -Isolated` | Windows Mono 开发包成功，84,286,506 bytes；产物不含项目 Editor/测试程序集，含可复用 UI 程序集，不含验证场景 |

XML/日志均在 `test-results/unity/` 的对应 `*-isolated.*`；图像为 Unity 摄像机/Canvas 离屏渲染，不是 Playwright/浏览器或真机截图。此任务不是 Web 应用，不启动 Vite 或改 HTML。

未验证实际刘海/挖孔屏安全区、微信包、触屏手感、目标机内存/性能、可商用字体和正式 Sprite/图集；已有组件提供适配入口，不把离屏比例测试当真机通过。完整分析器、CI、人工 Reviewer 仍未完成；正式 Player 安装缺项未修复，不重跑已知失败的非开发构建。

创作过程先在隔离副本调用 `-Task PreviewAssets` 创建资产，再保留 Unity 生成的 GUID 回写；随后通过 `PreviewLayout` 在 Prefab Mode 修复布局。重复创建会拒绝覆盖已有 Prefab，布局工具也不用于覆盖用户后续美术；后续直接编辑保存的 Prefab。主场景、现有未提交内容和用户打开的编辑器未被关闭/重置。
