# 救助站 UI 规范复核

2026-11-23，任务 `20261123-救助站UI复核-01a1f74d`。按当前[UI 排版与可读性规范](UI排版与可读性规范.md)检查实际 Prefab、运行代码和图形 PlayMode 画面。本轮只检查和留证，未修改业务代码、Prefab、资源或共享规范，未提交推送。

主要布局已符合当前方向，但不能判为全部达标：阻断性错误提示存在不可见问题，护理禁用说明还需补足。

## 发现与建议

### P2：保存失败后提示不可见，玩家不知道操作为何被停用

在站内调用既有保存失败入口 `StationPreviewSession.ReportPersistenceFailure()`，等待一秒并分别渲染 320×568、390×844、768×1024。错误文案已经写入 Toast，但有效透明度始终为 **0**，动物和外出操作已禁用；画面上没有可读的错误说明。

调用链为：`StationPreviewSession.cs` 第 127、438 行将 `_persistenceBlocked` 设为真，并先调用 `SetSuspended(true)`；[StationSceneChrome](../unity/HomelessPetHelper/Assets/Scripts/Modules/Station/UI/Habitat/StationSceneChrome.cs) 第 33 行同时冻结 Toast；随后 `SetStatus` 发送新消息。[ToastView](../unity/HomelessPetHelper/Assets/Scripts/Framework/UI/ToastView.cs) 第 34、47、71 行先将透明度清零，而暂停中的 `Update` 不再执行淡入。

这不满足规范 §3、§5.3 的关键信息可读要求。建议让阻断性错误使用可持续读取的提示/弹窗，且其显示不依赖被暂停的玩法反馈动画；保留停止业务操作的保护。验收应检查消息实际可见、完整后续指引可读，而非只断言字符串已赋值。

证据：[390 运行图](../test-results/station-ui-review/project/StationUiReviewEvidence/save-failure-390x844.png)、[首次复现报告](../test-results/station-ui-review/Followup.xml)。新增“阻断期间错误提示必须可读”的断言实际失败：预期透明度大于 0.9，实测 0。

### P2：护理按钮缺少各自动作的禁用原因

目前 [StationCareAvailability](../unity/HomelessPetHelper/Assets/Scripts/Modules/Station/UI/StationCareAvailability.cs) 仅向 UI 传递洗/梳是否可用两个布尔值。[StationPanelView](../unity/HomelessPetHelper/Assets/Scripts/Modules/Station/UI/StationPanelView.cs) 第 144 行据此禁用按钮；详情正文由 `StationPreviewSession.Condition()` 第 429 行生成，描述健康、脏污和是否接受轻触，并不描述独立的清洗/梳理权限。

在隔离副本中，将已有动物的护理权限设为“接受轻触、不接受清洗、接受梳理”，复用现有详情展示，等待 Toast 结束后检查三个比例。实测清洁禁用、梳理可用，正文仍只说明“主动靠近，愿意接受轻触”，没有清洁禁用原因。[390 运行图](../test-results/station-ui-review/project/StationUiReviewEvidence/independent-permissions-390x844.png)保留。此处是覆盖合法数据组合的检查夹具，不表示已修改正式动物数据。

初次进入时的“先查看健康情况，再安排护理”也只是会淡出的通用 Toast，不能作为各按钮的稳定说明入口。规范 §5.1 要求必要禁用原因在说明区可见。

建议沿用现有护理规则返回的失败原因，显示当前不可用动作的简短说明，或提供明确的按需查看入口；不增加大面积常驻文案，不改变护理权限。

## 已检查的合规部分

- 房间背景覆盖全 Canvas；交互 UI 使用安全区，顶部中央没有关键常驻控件。主屏只保留小菜单和外出操作，符合用户最新方向。
- 详情、菜单、护理与总结使用紧凑居中纸板，统一全屏黑遮；弹窗打开时不穿透到动物。长详情可滚动，关闭/结束操作保持可达。
- 所测普通按钮字号 48、标题 66；关闭和菜单热区 144×144，详情按钮 420×144。现有专项覆盖六比例及模拟安全区，护理动物不裁脚，总结保留动物图。
- 按钮沿用共享皮肤、动效和音效，视觉缩放与命中根分离；长列表两端的 Mask 动效已有几何/交互检查。
- “暂不接受触摸”状态仍可按抚摸，本轮实际反馈为避开伸来的手。这是一次被拒绝的尝试，不按“已经成功抚摸”误报为状态越权。
- 正文虽使用 `lineSpacing = 1.25`，但该参数还乘以字体自身的行度量，不能直接当作规范中的实际行高倍数。当前同字号单行首选高度与多行总高度也并非简单的 `字号 × 1.25`；所测正文未溢出，不仅凭这一组件参数列为违规。

## 验证与适用边界

- 本轮隔离副本图形 PlayMode 首轮 **13/13 通过**：既有站内 10 项、Toast 2 项和文字/几何测量 1 项。报告：[PlayMode.xml](../test-results/station-ui-review/PlayMode.xml)。该结果不代表新增异常路径也通过。
- 后续专门验证保存失败可读性，首次与补全权限夹具后的最终运行各 **1 项失败、0 跳过**，均实测透明度 0；最终[EdgeStatesFinal.xml](../test-results/station-ui-review/EdgeStatesFinal.xml)中权限按钮状态断言已通过，失败点仍是最后的错误提示可见性。保留失败报告，不放宽断言。中间一次探针直接访问模块 internal 属性导致编译失败，已只在隔离探针改为反射访问；`EdgeStates.log` 保留，不属于业务源码编译失败。
- [测量数据](../test-results/station-ui-review/project/StationUiReview.json)与[运行图目录](../test-results/station-ui-review/project/StationUiReviewEvidence/)保留。本轮原始 `history-*` 测量图未成功进入无记录时的总结，因此不作为总结证据；总结布局依据既有用例显式显示后的 `StationSceneEvidence/arrival-summary-*` 运行图及断言。
- [源码对比](../test-results/station-ui-review/source-comparison.json)检查了当前源工程 1433 个输入：1432 项与隔离副本逐字节一致；唯一差异是 Unity 测试运行将副本 `ProjectSettings.asset` 的 `runInBackground` 从 0 改为 1。业务脚本、Prefab 和资源一致，不将此称为全工程统一指纹通过。
- 小屏截图的抗锯齿会影响像素取样，本轮对比度抽样不构成完整的文字合成对比验收，不据此误报 4.5:1 违规，也不宣称全部对比度达标。
- 未进行手机物理触控、真机可读性、微信/AOT 或性能验收；未重跑全量测试，历史 854/854、139/139 不作为本轮结果。
