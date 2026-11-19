# 逆塔防 Unity 独立试做

## 本轮授权与范围

2026-11-19，用户认可 HTML 当前核心模式，允许将原型迁入 Unity；后续重点是难度、数值和单位/守方效果。此授权替代此前本聊天“暂不移植 Unity”，不替代正式平衡确认。

- 新建独立试玩场景和静态 Prefab，不替换 Main、停车棚案例、救助站或 HTML。
- 迁移三路出兵、双行动条/玩家窗口全冻结、场上自然/付费三合一、升级能力、真实范围横扫、持续守门、跨路射程及有限建塔/修塔。
- 纯 C# 规则不依赖 Unity；配置、文案、UI 和 Editor 创作工具分开。
- `src/siege/definitions.json` 为试验数值唯一源，转换工具生成 Unity 配置，文案同样从源表转换；不手改生成产物。
- 固定已开三路，不假装已经接入清路；成功只完成本挑战，不接正式接受救助、医护、回站、存档或微信验收。
- 静态 UI 保存后在 Prefab Mode 编辑，创建工具拒绝覆盖；现有未提交改动全部保留。

## 试玩入口

在 Unity 2022.3.62f3 打开 `unity/HomelessPetHelper`，菜单 **Homeless Pet Helper > Preview > Open Reverse Defense Trial**，然后 Play。

- 场景：`Assets/EditorScenes/Siege/SC_SiegeTrial.unity`。
- 面板：`Assets/Prefabs/Panels/Panel_SiegeTrial.prefab`。
- 池化 Cell：`Assets/Prefabs/Widgets/Widget_SiegeChoice.prefab`。
- 菜单只负责打开已保存场景，不重新创建、替图或修改 Main/Build Settings。

选手牌和上/中/下路后执行；不出兵可等待，换掉选牌独占本回合。场上点单位可选材料，重叠单位也能从“场上单位”列表选；付费合体选三个基础单位，可包括本回合“新出兵”，落点按钮在材料间切换。升级改道单位单独选择后开启改道，目标路线独立于出兵路线。射程查看直接点塔，不弹窗遮地图。

玩家窗口和手动暂停冻结全部战场。失焦/切后台也暂停，返回后明确继续，不补离线时间。同局重试保留种子/起点但更换会话身份；新种子与三个状态起点便于对照，不代表难度档位已平衡。

## 实现归属

| 部分 | 归属与边界 |
| --- | --- |
| 配置 | `src/siege/definitions.json`；生成工具 `tools/siege-unity-data.mjs` 输出 Unity DTO、不可变数值定义和随包 JSON。`--check` 检查漂移。更新源再生成，不手改输出 |
| 文案 | `src/siege/text.zh-CN.json` + `unity-text.zh-CN.json`；转换成 key/value 文本表，不混入数值表 |
| 规则 | `Scripts/Modules/Siege` 的独立无引擎 asmdef；目录内几何/供给/实体/命令会话/交战/动物职责分开，受控固定步长和会话内 Mulberry32 随机 |
| 数据 | 配置构建后不可变；可变单局仅由 `SiegeSession` 持有，UI 取脱离实时状态的快照，预览与提交共用原子校验 |
| UI | `Modules/Siege/UI`；静态 uGUI/显式绑定、共用 SafeArea、1080×1920，池化手牌/材料 Cell。只负责计划/输入/渲染，不直接写单局状态 |
| 战场 | 自定义 uGUI Graphic 绘制三路、塔/耐久、单位/护盾及射线；真实横扫圆与射程来自规则快照，改道画面使用规则的同一空间位置函数 |
| 创作 | `Editor/Siege`，只允许隔离批处理首次创建，存在资产即拒绝覆盖。布局微调通过 Prefab 内容编辑；后续在 Prefab Mode 改保存资产，不重复创建 |

继续沿用当前 HTML 的试验数值，不重开规则方向，不承诺正式配置生成框架已经选定。UI 目前明确为三个路线按钮；增加路线需调整对应视图，不是改张表就自动生成任意地图。

动物图复用已存在的 `animal.cat-orange.map` / `T_Animal_CatOrange.png`，不复制/生成/导入新图片，已在唯一台账补充使用场景；仍为 temporary / preview-only。拍击/横扫只有整图倾斜和范围/命中效果，不冒充爪尾分层动画；另一聊天的动作实验未接入。

## 验证

- 全量图形 EditMode **521/521**：原有规则/闭环493项，加本轮28项。
- 本轮28项包括18局 HTML→C# 回放：3种子×3状态×2策略，**1,616个检查点全部一致**；覆盖36次合体、20次完成修补及2次改道。逐点对照牌实例、单位位置/耐久/护盾/能力、塔状态、动物条/预告/锁区和结算。
- 定向规则另覆盖暂停/窗口冻结、同刻条满玩家优先、失败原子性/过期指令、预览不提交、受伤合体/指定落点、出生区不自然合体、丢牌、真实起点改道、超时。
- 图形 PlayMode **3/3**：真实手牌/路线/材料/落点/改道按钮完成种子20261119普通局，2次合体、1次改道，94.55秒游戏时间完成；包含点塔射线命中/射程查看、暂停、同局重试、换种子/起点和场景重载身份。
- 6种屏幕尺寸（375×667、390×844、540×1080/1170/1200、768×1024）×材料栏收起/展开，加模拟安全区；检查按钮边界/热区、文字、战场真实 Mesh 与非空像素，并人工查看截图。手牌槽宽固定，消费后不拉伸剩余牌；最后一次槽位修正已重新通过全部3项图形PlayMode及开发构建。
- Windows 64位 Mono **独立逆塔防开发构建通过**，包含该场景，不修改正式 Build Settings；项目 Editor/测试程序集未进入 Managed 目录。
- `node tools/check-unity.mjs`：203脚本/19程序集、0 issues；配置和回放 `--check` 通过；`npm run assets:check` 无漏登记/缺失，仍提示正式首批范围未确认。

最初全量 EditMode 未开启图形导致两个既有闭环截图用例空白；加 `-Graphics` 后521项全过，不修改其用例。界面验证发现并修复自定义战场缺少 CanvasRenderer；射程输入测试补齐真实 pointerPressRaycast 后通过，并非把失败断言删除。早期失败报告保留为过程记录。

未做：全仓旧 PlayMode 重跑、微信转换/真机/IL2CPP、低端性能、正式美术及平衡。快照和 UI 文本目前在20Hz刷新时有临时分配，不声称0 GC/真机60fps。

## 复现命令

```powershell
node tools/siege-unity-data.mjs --check
node tools/siege-unity-fixtures.mjs --check
node tools/check-unity.mjs
./tools/unity.ps1 -Task EditMode -Isolated -Graphics -ReportSuffix siege-all-graphics
./tools/unity.ps1 -Task PlayMode -Isolated -Graphics -TestFilter HomelessPetHelper.Tests.Siege.PlayMode -ReportSuffix siege-final-slots
./tools/unity.ps1 -Task SiegeBuild -Isolated -ReportSuffix siege-final
```

报告在 `test-results/unity/` 对应命令后缀；隔离副本保留截图 `SiegeEvidence/` 和 `Builds/SiegeTrial/`。本轮无可连接的 MCP Editor，未操作用户未保存场景；所有验证只针对已保存源码副本。没有提交/推送。

便于复核的本轮报告和截图另汇总于 `test-results/unity/siege-unity/`。最终图形报告 `PlayMode-isolated-siege-final-slots.xml`、全量规则 `EditMode-isolated-siege-all-graphics.xml`，最终开发构建日志 `SiegeBuild-isolated-siege-final.log`；早期失败日志不覆盖这三份结论。

## 下一步

先试玩 Unity 的选材/落点/改道与短屏表现，再调单位/炮塔/动物状态的数据和效果。当前普通局机械轮流分路仍可能简单、高警戒偏难；迁移一致不代表已经解决长期策略同质化。不重建本原型，不默认接 Main 或救助闭环，也不重复询问本次迁移是否开工。
