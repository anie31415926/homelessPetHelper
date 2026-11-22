# 紧凑关卡UI与水彩控件

2026-11-22，用户要求先收紧UI、减少大面积空白，随后生成并接入素材。本轮覆盖刚交付的小院清路/救助的暂停、声音设置、确认、目标、指引条和常用现场按钮。其他模块未批量换肤，不新增玩法和设置业务。

## 当前版本：手绘控件与统一全屏黑遮

用户选择手绘版暂用，并要求半透黑遮统一、不再只遮局部。当前运行页替换为512×360、82893bytes（81.0KiB）的手绘14切片；GUID、切片布局与滑块修复保持。[生成来源及接入](../art-source/ui-handdrawn-20261122/README.md)、[清新版原页保留](../art-source/ui-soft-20261122/preserved/T_UI_LevelControls.png)。仍为temporary，观感接受不代表许可或渠道验收。

原覆盖层在面板SafeArea内，导致刘海/挖孔与底部手势区漏遮。现将保存的Widget_LevelOverlays放在各面板Canvas末尾：GuideMask、ModalBackdrop覆盖整个画布，SafeContent复用SafeAreaView容纳原卡片、说明和提示。黑遮使用纯黑88/255（约35%）；暂停/设置/目标/确认只共享一个遮罩，子层切换不加深，最后一层关闭后撤掉，模态弹窗期间隐藏引导遮罩。指引原有高亮孔和安全入口仍跟随目标坐标；普通提示不加黑遮。统一约定已写入[UI规范](conventions/topics/ui.md#当前工程的全屏遮罩约定)。

通过隔离副本Prefab Mode增量迁移并只取回三个Prefab，原有业务绑定、控件ID及贴图meta保持，新增显式_modalBackdrop绑定；运行时只切换既有对象。迁移工具是一次性的，不重跑LevelOverlayAuthor或整套换肤器。当前仅小院清路/救助共享覆盖层，其他模块未批量换肤。[本轮任务与实际验证](协作任务/20261122-手绘接入与全屏遮罩-01a1f74d.md)是最新证据，以下全量数字均属历史快照。

当前Unity只读审计确认Main仍22张图片，源图1741548bytes（1700.7KiB），比清新版多33.3KiB、超过原1536KiB源规划164.7KiB；未提高预算。同512×360的RGBA32基础像素仍720KiB，关闭读写/mip，未新增图集或加载框架。源图字节不等于微信下载。[本轮审计](../test-results/fullscreen-ui/image-resources.json)。

最终统一有图形验证运行`20261122T062440793Z-e126e7f2-ac58-4cd0-80f8-e0eb9f5ff203`通过：源码检查380脚本/36程序集无问题，EditMode **791/791**、PlayMode **83/83**，无跳过，两个Unity进程正常退出。1205项保存输入及两个隔离副本一致，指纹`9cd129d49680807437cd339e1ffbe3b8dbf909bc70a6b2ab28df9628881c3c52`。[最终报告](../test-results/unity/verify-20261122T062440793Z-e126e7f2-ac58-4cd0-80f8-e0eb9f5ff203/summary.json)、[102张实际截图与逐图哈希](../test-results/fullscreen-ui/final/provenance.json)、[暂停修复前后](../test-results/fullscreen-ui/final/pause-before-after.png)、[声音设置](../test-results/fullscreen-ui/final/settings-390x844-cutout.png)。截图为5种比例和模拟挖孔，非真机；实际检查暂停/设置/确认/指引/救助暂停。原顶部/底部漏遮条的像素对比全部变暗，见[边缘对比](../test-results/fullscreen-ui/edge-pixels.json)。四角覆盖及射线拦截、单遮罩、多层返回和最后一层撤罩均通过。微信/真机/性能未验，未提交推送。

## 前版保留：清新可爱风格与滑块修复

用户随后反馈上一套画风生硬且声音滑块仍有显示问题，现已重新生成并替换为奶白、嫩绿、浅杏、圆润全彩图标及爪印圆滑钮。运行页为512×360、48758bytes（47.6KiB）、14切片；原3个九宫格与8个图标的SpriteID/内部ID、贴图GUID保持，新增3个音量专用切片。静态Prefab现有对象ID及显式业务绑定不变。[新批次与5个真实ComfyUI作业](../art-source/ui-soft-20261122/README.md)、[当前任务与最终验证](协作任务/20261122-清新UI与滑块修复-01a1f74d.md)。

滑块的填充、轨道与滑钮统一端点范围；零高度移动父节点承接Slider驱动锚点，圆形滑钮使用Simple并保持比例，不再将纸卡九宫格挤成方块。横向Filled图像保证0音量不残留填充，空轨道单独加深，滑钮承担按下反馈。原音量/静音、保存/取消/恢复默认及多层弹窗业务保持。仅通过已保存的Prefab Mode增量编辑，未重跑原创建器或旧整套换肤器。

已验证旧版端点错位回归失败、过浅轨道对比回归失败；修复后局部PlayMode3/3、UI EditMode11/11，覆盖三条滑块的0/1/25/50/99/100%、实际渲染网格和Pointer拖动75%，并核对音频偏好。五种比例与模拟挖孔的运行画面均检查安全区、文字完整及控件相交；最终统一运行20261122T041423300Z-55fb528d-f55f-48dd-9d61-612f952614bc已通过EditMode791/791、PlayMode82/82；1203项输入一致，指纹`0a4a97d14d110d8be1f5b3347e1740ed7589e5cbc969ca64dd11460a8228101d`。见[最终报告](../test-results/unity/verify-20261122T041423300Z-55fb528d-f55f-48dd-9d61-612f952614bc/summary.json)、[90张截图来源](../test-results/soft-ui/final/provenance.json)、[新旧设置对比](../test-results/soft-ui/final/settings-before-after.png)与[音量状态](../test-results/soft-ui/final/volume-states.png)，不套用以下历史数字。用户已认可并要求保留此版；当前已另存源页供回退，后续手绘选择与验证见上方最新版本。

新页源比首轮减少21.8KiB；RGBA32基础像素由544KiB增至720KiB。Main仍22张图片源，共1667.4KiB，超原1536KiB规划131.4KiB；源图、导入像素和微信下载分开，不涨预算。temporary/first-play、正式许可/微信/真机触控/性能未验边界继续保留。试玩入口不变：Main→外出→小院→右侧暂停/目标。

## 独立手绘候选

用户要求保留清新版并再试手绘。已完成彩铅/细线稿/浅纸纹候选，512×360/14切片/81.0KiB，仅新隔离副本替纹理；Unity源工程1203项输入不变。当前运行版继续为上方清新版，不新增运行时换肤框架。候选PlayMode3/3和90张截图已完成，非新的全量或真机验证。见[左清新右手绘设置对比](../test-results/handdrawn-ui/final/settings-soft-vs-handdrawn.png)、[暂停对比](../test-results/handdrawn-ui/final/pause-soft-vs-handdrawn.png)、[候选与来源](../art-source/ui-handdrawn-20261122/README.md)及[任务](协作任务/20261122-手绘UI候选-01a1f74d.md)。

## 首轮紧凑布局记录（历史快照）

以下11切片、69.5KiB及790/81数字仅对应本页首轮，当前运行图与后续验证按上方入口读取。

## 布局原则与实现

参照本项目[UI规范](conventions/topics/ui.md)的静态绑定、安全区、触摸热区与独立文案，以及[资源规范](conventions/topics/assets.md)的九宫格与共同使用边界。采用常见竖屏休闲游戏的紧凑居中卡片结构；没有复制外部项目的图片、代码或受版权保护的界面。

旧版四类页面均占安全区约74%高度，确认页仅两行说明也留有大段空白。现在页面只在横向锚定，内部单层VerticalLayoutGroup+ContentSizeFitter按内容确定高度；标题、正文、操作依次排列，16设计单位间距、36上下内边距。按钮116单位高；设置三行144单位高、滑杆热区约91单位，不通过整体缩小UI挤压点击目标。

暂停页只保留标题及三项操作，确认页说明紧接双按钮；目标正文去掉重复空行，按钮紧接正文。指引从大矩形改为按一两行文字决定高度的短纸条。设置页说明保留，保存错误才显示状态行，取消错误后不占空位。页内文字独立、正常字体不使用BestFit缩小来掩盖裁切。

复用原PopupStack、GuideMask与业务绑定，子层关闭保留父层和暂停。运行时只填数据/显隐；Editor/LevelUi/LevelOverlayStyler只在隔离副本Prefab Mode增量调整已保存资产，不重建旧面板。不重跑前轮LevelOverlayAuthor；后续在保存的Prefab Mode编辑。

## 水彩素材

[本批图片与完整来源](../art-source/ui-watercolor-20261122/README.md)：本地ComfyUI四次生成无字纸底板、鼠尾草绿主按钮、暖米色次按钮及8功能图标，派生11切片的512×272共享透明页。71132bytes源PNG，导入RGBA32基础像素544KiB；关闭mip/读写，九宫格扩展，不为不同弹窗切整张图。现有uGUI与已安装2D Sprite包，无新依赖安装或全局大图集。

图集静态绑定Widget_LevelOverlays、Panel_Approach与Panel_SiegeTrial。只有现有小院使用新覆盖层；其他旧入口延续原行为。台账维持temporary/first-play，母图candidate/preview-only，加载计划不自动意味着按需加载。Main22张图片源合计1689.2KiB，源规划线超153.2KiB；暂不提高原预算，不将源字节/PNG压缩等同微信下载。微信/正式许可与性能仍待测。

## 验证

局部回归2/2通过，覆盖五种比例及模拟挖孔的暂停、设置、保存失败、默认确认、回站确认、目标、首次两种清路指引和救助部署指引；断言可见文字完整、控件不相交、短弹窗不超过安全区55%、页面在安全区内。初轮发现设置标签高度不足，已修复；截图复核又修正Slider驱动anchor造成填充与滑块拉高的问题。

基础检查377脚本/36程序集通过；工具回归35/35，官方Editor程序集3种边界夹具通过（Editor依赖允许、runtime引用拒绝、包未安装拒绝）。资源台账检查无缺失/漏登记，Unity只读审计确认新纹理由Main引用。

最终统一验证已通过：运行`20261121T200645021Z-dad942d6-63a0-4da7-95ea-f8da32d82783`，见[最终报告](../test-results/unity/verify-20261121T200645021Z-dad942d6-63a0-4da7-95ea-f8da32d82783/summary.json)。EditMode **790/790**、PlayMode **81/81**全部通过，无跳过；1199项保存输入与两个隔离副本核对一致，两个进程均正常结束，基础源码检查377脚本/36程序集无问题。源码指纹`8057af322eac21c1571fc15e4b6c669bf2c7c37d42f4c36128318f426696a353`。

[54张最终运行截图](../test-results/compact-ui/final/)及[来源/逐图哈希](../test-results/compact-ui/final/provenance.json)保存五比例及模拟挖孔状态；已人工查看最终声音设置、暂停及默认确认，并制作[左旧右新的确认页对比](../test-results/compact-ui/final/confirmation-before-after.png)。对比图左侧来自前轮已验证截图，右侧来自本轮最终PlayMode；它是截图并排，不是新生成的游戏画面。

首轮统一运行20261121T200336528Z-06144e19-f337-4cb0-be83-85c77f91c883的790项EditMode全部通过、Unity正常退出，但包装层写EditMode.json遭瞬时文件占用而失败；保留失败报告，未当作整轮通过，随后使用新运行ID重验。未做微信转换/AOT/真机、触控体验或性能验收。未提交推送。

试玩：Main→外出→小院，右侧暂停/目标，暂停→设置→恢复默认可体验三层返回。首次机制会显示短指引，旧学习记录保留。
