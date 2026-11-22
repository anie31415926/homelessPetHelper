# 现有 UI 规范检查与整改

2026-11-22，按[UI 排版与可读性规范](UI排版与可读性规范.md)整改。对应[任务与执行记录](协作任务/20261122-UI规范整改-01a1f74d.md)。已检查14个UI Prefab并整改其中13个，最终统一验证通过；保留前轮工作区改动，未提交推送。

## 问题与实现

| 范围 | 检查发现 | 本轮处理 |
| --- | --- | --- |
| 共享排版 | 正文、菜单与旧入口大量使用22–34设计字号，部分BestFit继续缩小；固定父行高不能容纳新字号 | 保存Prefab统一字号角色、关闭BestFit、深色墨字，文字报告自身高度，操作行按内容换行 |
| 逆塔防底栏 | 卡图、名称、费用挤在横排，主动作/撤销与手牌重叠 | 静态卡片分区；费用图形+动态数字，与HUD常显“资源”图例一致；上下文动作独立占位，手牌按规则容量重排，主动作位置稳定；路线选择使用独立临时操作行，随既有预览显示对应通道，消除场景缩小时三路热区互相覆盖 |
| 关卡HUD | 平板比例下意图底板遮挡目标入口，空提示仍占用场景 | 意图提示按共享间距排在暂停/目标下方，场景同步预留顶部；无提示时收起占位，新增六尺寸HUD重叠检查 |
| 手牌状态 | 价格位数、余额变化、复用残留与不同状态需要区分 | 0、一/两/三位动态价格；资源不足文字，选中标记与已计划文案，空手牌提示；Bind复位卡片显示状态 |
| 清障 | 旧绝对位置与底部多按钮挤占场景 | 顶角菜单144热区，工具按实际文本宽度换行，场景为底部操作预留空间 |
| 共享弹窗 | 旧固定高度与新字号冲突，设置页长内容出屏 | 内容驱动高度，长正文/音量控件在内部滚动；标题与确认按钮固定可达，缓存同时考虑页脚变化；保留单层全屏黑遮与原弹窗栈 |
| 地图 | 图片和标签随可用高度缩到不可读，部分热区不足144，名称/状态被裁 | 保留地点锚点/美术宽高比，标签使用独立尺寸，地图与说明可滚动，出发/返回固定；射线命中和热区逐比例验证 |
| 救助站 | 页头随高度比例被压、详情增高后下半部分移出屏幕，护理动作固定行高 | 固定可读页头并预留正文；详情从底部展开并随内容刷新，动作行换行；护理内容保留720设计单位起点，较短视口可滚动，较高视口填满画面；旧护理入口同样保留有效画面高度 |
| Editor备选入口 | 共用卡片/字号增大后，旧试玩UI固定行高/可视空间不足 | 增量修改已保存Prefab，长页滚动；动作预览子Canvas同步Match Width，下拉列表限制在安全区且选项可滚动；原规则、绑定、图片与入口保留 |

费用小图形由现有uGUI Graphic绘制，不新增币种、价格语义或位图，不占图片台账。扣费时机、手牌数量、救助规则与存档未改。

## 后续编辑入口

- 参数唯一来源：`Assets/Settings/UI/LayoutMetrics.asset`；对应`Framework/UI/UiLayoutMetrics.cs`。采用1080设计基准、CanvasScaler明确Match Width、48边距/24间隙、144热区、240×384卡片等起点。这些是设计单位，**不等同手机dp/pt或已验物理尺寸**。
- 可复用的按钮换行、内容弹窗、最小热区、下拉边界和比例内容高度分别在`Framework/UI`；手牌容量布局在`Modules/Siege/UI`。运行时使用静态绑定，未按名字查控件或拼面板。
- 本轮author只在保存的隔离副本中调用PrefabUtility增量编辑，不重跑原面板创建器。`test-results/ui-layout-migration/before/`留存本轮写前版本；迁移脚本只用于这一版本的离线迁移，**后续请继续在Prefab Mode编辑，不对已迁移Prefab叠加运行**。
- 动效、暂停、引导及多层确认沿用已有所有者；可见裁切区只影响滚动，原业务验证仍保留。

## 验证与边界

最终命令从仓库根执行：

```powershell
node tools/verify-unity.mjs --task docs/协作任务/20261122-UI规范整改-01a1f74d.md --session 01a1f74d-412d-7613-a27d-fae91bb606ef
```

本机使用Windows Node v24.14.1、PowerShell 5.1及Unity 2022.3.62f3，全部使用新的图形隔离副本，不占用户Editor。

| 检查 | 最终结果 |
| --- | --- |
| 基础源码检查 | 405脚本、36程序集、零问题；不是完整C#分析器 |
| 全量EditMode | 800/800，失败/跳过/未决均0 |
| 全量PlayMode | 90/90，失败/跳过/未决均0 |
| 保存输入 | 1269项，源工程与两个隔离副本前后匹配 |
| 本轮变更 | 对写前基线33项修改、28项新增、0删除；全部位于申报范围，Art无改动 |
| 运行截图 | 最终PlayMode副本15个Evidence目录共506张，其中UI专用84张；含原有回归截图，不是506张均为新UI |

唯一最终报告：[summary.json](../test-results/unity/verify-20261122T100507826Z-566b5f77-0d84-49a4-9a88-2401a4bbba87/summary.json)。输入指纹为`2a2b3cb9beb2128b6d25c92657dd337eb708fbe1183d56f37ccfe43caa4dbcdf`，结束时间2026-11-22T18:13:26+08:00。输入清单、阶段报告、XML/日志和工具哈希在同一运行目录索引；[本轮输入差异](../test-results/ui-layout-migration/changed-inputs.json)区分前轮已有修改。

较早的统一运行及`targeted-*.xml`、`play-review.xml`保留用于排错。第三次统一运行虽通过，随后截图发现4:3意图遮挡目标，已经修复并加重叠检查；**上述最终第四次运行**才对应交付代码，不沿用早先快照。

运行截图覆盖320×568、360×640、390×844、432×960、540×1080、768×1024；另沿用原比例/模拟安全区、引导、真实救助/护理、滑块、暂停与连续状态回归。滚动内容检查逐个滚入可见范围，文字仍在完整尺寸下测量，未简单豁免滚动区。

微信转换、AOT、手机物理触控、刘海/挖孔/Home条实际机型、正式美术对比和人工玩法体验未验；编辑器截图不代表渠道或目标机验收，后续需要目标微信机型实测。尚无独立人工Reviewer签字，不用于直接发版。

## 看图与试玩

现有Main入口继续使用`Assets/Scenes/Main.unity`；地图→选择小院→清障→救助，暂停内可打开设置；救助回站后选择动物检查/护理。备选Editor入口保留。

以下均来自最终统一PlayMode副本，本机保存在`test-results/unity/isolated-PlayMode-083502cb53114f7881a3d01ca34e8589/`：

| 页面 | 截图 |
| --- | --- |
| 救助底栏 | [390×844](../test-results/unity/isolated-PlayMode-083502cb53114f7881a3d01ca34e8589/UiLayoutEvidence/rescue-390x844.png)、[4:3兼容布局](../test-results/unity/isolated-PlayMode-083502cb53114f7881a3d01ca34e8589/UiLayoutEvidence/rescue-768x1024.png) |
| 路线选择 | [三路独立热区](../test-results/unity/isolated-PlayMode-083502cb53114f7881a3d01ca34e8589/YardFieldEvidence/choose-route-0-390x844.png) |
| 清障 | [320×568](../test-results/unity/isolated-PlayMode-083502cb53114f7881a3d01ca34e8589/UiLayoutEvidence/opening-320x568.png) |
| 地图 | [标签与固定出发区](../test-results/unity/isolated-PlayMode-083502cb53114f7881a3d01ca34e8589/UiLayoutEvidence/map-390x844.png) |
| 设置 | [320×568内部滚动](../test-results/unity/isolated-PlayMode-083502cb53114f7881a3d01ca34e8589/UiLayoutEvidence/settings-320x568.png) |
| 救助站 | [动物详情](../test-results/unity/isolated-PlayMode-083502cb53114f7881a3d01ca34e8589/UiLayoutEvidence/animal-detail-390x844.png)、[护理](../test-results/unity/isolated-PlayMode-083502cb53114f7881a3d01ca34e8589/UiLayoutEvidence/animal-care-390x844.png) |

当前已支持配置的UI完成编辑器检查；超大手牌、未接入语言及未来配置仍需随实际内容追加容量验证。下一步先收集真实手机可读性和操作反馈，后续在Prefab Mode局部编辑。
