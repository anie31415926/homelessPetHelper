# 手绘UI素材与临时接入

## 当前选择与源工程接入

用户已选择手绘版暂用，候选页现复制为Unity运行页，保留原GUID与14切片。清新版PNG/meta/切片已保存至[保留目录](../ui-soft-20261122/preserved/)，源页SHA256为97e5fefd165ef0d85513386c8d114c1924ca2f55225c54fb35c64fabb5f77887。接入同步修复全屏黑遮和安全区层级，保持滑块与现有业务。[导入记录](unity-import.json)、[实现及验证入口](../../docs/紧凑UI与水彩控件.md)、[本轮任务](../../docs/协作任务/20261122-手绘接入与全屏遮罩-01a1f74d.md)。

唯一运行条目为ui.level.controls（temporary/first-play），ui.handdrawn.controls保留派生候选，不重复算入Main首批。当前视觉accepted，rights/target仍pending；未再次生图、没有新增模型。以下“候选不覆盖源工程”“当前清新版”等为选择前的真实历史，不能当当前运行状态。

接入后的完整验证：EditMode791/791、PlayMode83/83，1205项输入匹配，指纹`9cd129d49680807437cd339e1ffbe3b8dbf909bc70a6b2ab28df9628881c3c52`，运行`20261122T062440793Z-e126e7f2-ac58-4cd0-80f8-e0eb9f5ff203`。[最终报告](../../test-results/unity/verify-20261122T062440793Z-e126e7f2-ac58-4cd0-80f8-e0eb9f5ff203/summary.json)、[102张实际运行截图](../../test-results/fullscreen-ui/final/provenance.json)。台账检查无错误/缺失/漏登记，Unity审计Main仍22图源1741548bytes（1700.7KiB），超原1536KiB源规划164.7KiB未提高预算；同尺寸RGBA32基础720KiB不变。微信下载/真机仍未验。

## 选择前候选记录

2026-11-22。用户认可清新UI并要求保留，再尝试手绘风格。本批独立生成候选，不覆盖Unity源素材/Prefab。沿用本机ComfyUI与现有模型；提示和引用见[jobs.json](jobs.json)，后续保存每项工作流、提交历史、母图和派生依据。

目标：浅色纸卡、细灰绿铅笔轮廓、彩铅填色、轻水彩笔触，保留紧凑布局和音量滑块修复。现已完成独立候选和实际界面对比，临时预览候选，非正式许可或渠道验收。

## 候选边界与复现

清新版当前运行页SHA256为`97e5fefd165ef0d85513386c8d114c1924ca2f55225c54fb35c64fabb5f77887`，保持原地；保留14切片、静态Prefab、文字与音量业务。本批纹理候选仅输出到`candidate/`，实际预览只在`test-results/handdrawn-ui/project`的新隔离副本替换纹理，不修改源工程或此前通过的快照。

本机ComfyUI0.37.1，既有Qwen Image2.1 INT8 ConvRot / Qwen3VL8B / Qwen VAE。形状控制图为第一参考、小院实际背景为第二风格参考，resolution0、shift3.1、24步、CFG4、euler/simple、denoise1；seed2026112281—2026112286，见实际workflow.json。未下载模型、未用OpenAI CLI/API。原始母图不带alpha，候选将按派生脚本去底，不称原生透明输出。

paper/primary/secondary/knob已检查；首稿symbols额外生成了各自外围方框，保留原始图与作业记录，改为symbols-v2明确禁止外围框并重新生成。两稿都登记，只有选定稿参与候选页；不会覆盖初稿历史。

## 成图与实际界面对比

6次真实ComfyUI作业均完成，最后使用paper/primary/secondary/symbols-v2/knob五项；symbols初稿保留追溯。作业ID：

- knob：`3fb6a50f-2e6d-4fde-b51f-7d9c79e8effa`。
- paper：`20d7e4f2-cb8f-4eac-8113-2709ce735786`。
- primary：`9c7074b4-41f1-4d9c-a97d-2210f2f469f7`。
- secondary：`6812fe1b-002d-4c7a-80f0-3e484eb6cd1d`。
- symbols：`3b02028b-a548-44e3-b2de-92a8532e4af8`。
- symbols-v2：`66b64d7b-74dd-4f9f-8291-660d2551027c`。

[最终候选页](candidate/T_UI_LevelControls.png)为512×360、82893bytes（81.0KiB），14切片，SHA256`fea0e89c46c0c53a02864049b40b57238a8d8c219f07d9671aad60cac31c9219`。原图去底、组件保留、裁切、Lanczos缩放、独立轨道派生与切片坐标见[派生记录](derivation.json)和[脚本](derive.mjs)。所有源图/母图/参数在此批次，白底去除后保留alpha，未伪称原生透明生成。

[切片检查](../../test-results/handdrawn-ui/candidate-check.json)确认与清新版14切片几何逐项一致、边界有效且各含透明轮廓；已人工检查母图、深底边缘和Unity实际的暂停/设置/0、50、100%音量。候选源比当前47.6KiB多33.3KiB，超过本项72KiB计划9.0KiB；保持原规划，不因候选涨当前首批预算。两页尺寸相同、基础RGBA32均720KiB，不代表实际内存/下载。Main仍保留清新版，22图源1667.4KiB不变，未将候选视为正式首批。

隔离预览于2026-11-22T12:31:57+08:00完成：Unity2022.3.62f3，有图形PlayMode **3/3**通过、无跳过，复用既有弹窗/指引与三条滑块端点、填充网格、0/1/25/50/99/100%、Pointer拖动回归。[运行记录](../../test-results/handdrawn-ui/render.json)与[90张截图/逐图哈希](../../test-results/handdrawn-ui/final/provenance.json)。这是候选局部验证，不借清新版全量数字称手绘候选通过全量。五种比例及模拟挖孔不是实际设备证据。

- [暂停左清新右手绘](../../test-results/handdrawn-ui/final/pause-soft-vs-handdrawn.png)。
- [设置左清新右手绘](../../test-results/handdrawn-ui/final/settings-soft-vs-handdrawn.png)。
- [手绘0/50/100%音量](../../test-results/handdrawn-ui/final/volume-states.png)。

[源工程保持检查](../../test-results/handdrawn-ui/source-preservation.json)逐项确认前轮1203项输入无变化，指纹`0a4a97d14d110d8be1f5b3347e1740ed7589e5cbc969ca64dd11460a8228101d`对应的清新版完整保留；未覆写旧验证快照。图片台账命令`node tools/resource-catalog.mjs`通过，无缺失/漏登记；预览形状与清新版相同、个别预览项超计划、原首批与许可未验的review提示保留。最初形状条目只填reference被校验拒绝，已明确登记为计算生成的candidate控制图后重验通过，没有把失败报告当成功。

仅停止本轮拥有且确认空队列的ComfyUI进程80636，保存[停止记录](../../test-results/handdrawn-ui/service-stop.json)。没有模型安装，没有改Desktop配置。当前candidate/preview-only，待风格选择；许可/微信/真机/性能未验，未提交推送。
