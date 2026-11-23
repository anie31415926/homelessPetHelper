# Loading 场景与拆层动效

2026-11-23；用户授权按附件拆出原元素，新增加载场景，不改玩法与Main内容。

## 限定提交验证

2026-11-23用户要求只提交本部分、不推送。提交候选从HEAD `71fd4c4`局部合并Loading，不含其他会话BGM、地图/回访、站内或pet-sit-*台账；母图/生成参数/历史及运行资源同批，排除本机服务启动器与临时验证目录。候选基础源码436脚本/37程序集0问题，台账退出0，真实图形EditMode7/7、PlayMode13/13，报告在`test-results/loading-commit/`。

候选严格原始输入门禁退出1，唯一差异是Unity将旧地图图集CRLF转为与HEAD完全相同的LF；没有内容变化，图集不在本次提交范围。1359项其余输入不变，41项Loading与最终源范围保持同内容，JSON/Prefab两项Git换行归一化已单列，详见`verification-review.json`；保留原失败报告，不称整个门禁通过。来源/预算、旧作者工具弃用警告和微信/AOT/真机/真实Bundle下载未验边界不变。

## 正式启动接入

2026-11-23用户已认可本版形体/画面，要求正式流程接入且解耦；未打Bundle时可演示进度。当前Build Settings、DesktopBuild和ProjectSetup的首入口均为`SC_Loading → Main`，沿用而不另建Boot，不修改Main玩法/存档。直接在Editor打开Main按Play仍是开发快捷入口，会跳过启动Loading；完整流程从`Homeless Pet Helper > Loading > Open Loading Scene`按Play，构建包按首场景自动进入。`Preview Animation`仍只停留看动效，不执行准备/切场景。

当前默认准备演示3秒，使用unscaled时间推进0–90%；再通过真实Unity异步场景装载推进90–99%，只有目标就绪后显示100%一帧再激活Main。演示不是Bundle下载/解压进度，不新增真实资源打包/下载框架。场景`Loading Root`的`_demoSeconds`可设0取消演示，`_timeoutSeconds`默认60秒限制各准备/场景等待阶段。配置经隔离`LoadingStartup` Editor API保存到既有场景，未重建静态Prefab或换图。

### 接入边界

- `LoadingView`只消费进度/失败，`LoadingArtworkMotion`只播动效，均不依赖资源API或Main。
- `ILoadingTask`是局部准备任务契约：主线程`Tick(unscaledDeltaSeconds)`，只读归一化`Progress`和成功完成`IsComplete`，`Dispose`幂等取消/释放任务拥有的句柄；准备异常抛回场景Owner统一显示失败。
- `DemoLoadingTask`纯C#，不引用Unity/UI；`UnitySceneLoadingTask`单独适配实际场景预加载、激活门闩和不能取消的Unity操作，不持有UI。
- `LoadingSceneRoot`负责一次装配、阶段顺序/进度单调/超时和生命周期。未注入时默认演示，真实资源准备实现后，在其`Start`前由启动装配代码调用`ConfigurePreparation(realTask)`即可替换，不改View、动效或Prefab。不引入静态单例/Service Locator/全局框架。

`ConfigurePreparation`仅成功调用后转移所有权；空任务、重复配置、预览模式和启动/停止后的注入均拒绝，拒绝的任务仍由调用方释放。准备成功后先Dispose任务，再开启场景装载：将来Bundle需要跨Main保留的资源必须在`IsComplete`前交接给明确的后继资源Owner，不能在任务Dispose时卸载Main仍需要的资源。失败/禁用/销毁均释放一次，迟完成不能更新旧UI；Tick内同步禁用也不会发布迟到进度。组件禁用后不会自动重新开始，重试应重新进入Loading场景。Unity场景操作不能撤销，释放会打开激活门闩避免0.9阻塞队列，不把它声称为场景加载已取消。

注入准备任务可能先加载包含目标场景的Bundle，因此目标是否可装载的检查在准备完成后进行；默认演示模式可提前检查Build Settings。测试确认准备未完成时不会提前报目标缺失，完成后仍严格校验、不跳过失败。

本轮最终Loading专项13/13 PlayMode、7/7 EditMode，覆盖默认3秒演示→真实Main、替换准备任务无额外演示、延后目标校验、失败/NaN/超时、重复配置/所有权、外部及Tick内取消、Unity门闩释放。最终默认启动记录200项0→1单调采样；最终实际画面仍与已认可图一致。固定副本完整EditMode854/854；PlayMode135通过、1项旧rig A/B因复制时漏带基线而跳过，补齐同哈希基线后该项独立重跑1/1通过，0失败。原135/136报告不改，不冒称单次136/136或0跳过。

最终固定副本`test-results/loading-startup/final-snapshot`的1408项Assets/Packages/ProjectSettings输入前后不变，指纹`db21a0043a8f4179b8ff60247ed96e588631c75d813caba53125ca256a83ca21`。本任务41项Unity输入与副本一致，范围指纹`cfaf7165c8f051e7fcf8f0c44da3a98c0d88950e75251830f38dab22bc3c762c`；两种口径不混用。报告在`test-results/loading-startup/final-EditMode.xml`、`final-PlayMode.xml`、`baseline-completion.xml`、`final-inputs.json`和`source-comparison.json`。共享工程统一入口运行`20261123T113140102Z-e59300a1-9bf7-465e-8521-89a63e326ffd`因进行中Toast源码格式退出1，未进Unity，失败保持；不代改其他任务，不称当前共享整工程门禁通过。

本轮首次冻结458脚本/37程序集基础检查0问题；后续共享工程其他会话新脚本格式提醒不代修。编译仍有旧一次性LoadingSceneAuthor的spritesheet弃用警告，无本轮新增C#警告/错误。已认可PNG、Prefab、View和ArtworkMotion五项与美术交付版哈希完全相同；场景仅新增3秒/60秒配置，台账本命名空间视觉accepted，来源/目标pending、temporary和预算保持。19:53本轮范围释放、未提交推送，未打Bundle/新增资源框架；微信/AOT/真机和真实下载仍未验。

## 当前重绘版

### 动物单体形体修正（18:45）

用户指出上一版动物混入奇怪内容，确认不统一猫狗，要求生成完再检查形体姿态。已改为16切片逐项生成与检查，保留兔子/龟/雪貂/豚鼠/仓鼠/龙猫等小宠；本轮25个实际ComfyUI作业，最终取12项v2、3项v3、1项v4，夹带动物、错误耳形/毛色和头像附身体的版本都保留但不接入。完整提示/作业ID及逐项观察见[单体计划](../art-source/loading-comfy-20261123/individual/plan.json)、[视觉检查](../art-source/loading-comfy-20261123/individual/review.json)与[静态检查图](../art-source/loading-comfy-20261123/individual/review-sheet.png)。这是Q版形体视觉复核，用户观感与来源/目标端仍pending，不称解剖准确性认证。

先检查正向母图/深浅底透明派生，再光栅转向配合圆环；派生build对16项审核状态和母图SHA校验，避免审核后悄换图。Prefab Mode仅更新切片矩形并保留32个spriteID，非动物16切片原RGBA像素、纸纹背景/进度条/小屋和加载/动效C#均保持。新页1024×864/816881bytes，背景340675bytes，两图1157556bytes；比前轮少26580bytes，仍保留源预算超额提示，不等于微信下载量。上一版运行文件在`individual/prior-runtime/`留存，首轮抠图的`preserved/`不覆盖。

本轮专项EditMode2/2、PlayMode5/5，最终统一`20261123T104851671Z-09f00d79-33d5-4735-90ff-9b0b3592947e`为passed：1400项保存输入、指纹`d5b4bc95a1d2f670c12c28fe3359f225349cab5018e8f6d9f105589f7bedb510`，图形EditMode849/849、PlayMode128/128，0跳过、退出0，源/两副本/报告核对一致。454脚本/37程序集基础源码0问题。本任务33项Unity输入与最终两副本一致，范围指纹`698b52c09be345c904b37338316650c0a5e3b5af434802a853db300b1e5e2f31`；[范围比对](../test-results/loading-comfy/individual/source-comparison.json)。此前一次在849项EditMode后因其他任务完成改变协作上下文而failed，原报告保留，不混用为最终通过。

最终副本`isolated-PlayMode-06af9c049397498885669960d1cc9938/LoadingEvidence/`有6比例18张实际截图、48帧旋转；已查看短屏/长屏/平板姿态和深浅底透明边。导出的[4000ms动效片段](../test-results/loading-comfy/individual/loading-preview.gif)、[适配联系图](../test-results/loading-comfy/individual/responsive-contact.png)与[逐帧哈希](../test-results/loading-comfy/individual/render-provenance.json)对应本轮最终图，不是完整绕圈周期。脚本断言16个非动物切片RGBA和背景不变；32个spriteID/internalID逐项一致。台账检查退出0、无缺失/漏登记，预算与未批准提示保留。下文848/124是前轮历史，不冒用。独立8195本轮PID31464核对空队列与命令行后停止，没有付费API/新模型/插件。18:58路径释放、未提交推送；用户观感/许可/微信/AOT/真机和真实首启下载仍待验证。

### 前轮五项重绘历史

用户随后反馈毛边与进度条塑料感，明确改用ComfyUI重绘。当前已替换为5个真实本机Qwen Image2.1作业派生的分层素材，不再从小原图抽取运行像素。细铅笔闭合轮廓、浅水彩/彩铅纸纹，进度条为哑光纸条和独立填色，没有高光倒角。来源、完整工作流/作业ID、母图与后处理见[本轮批次](../art-source/loading-comfy-20261123/README.md)。用户的“避免毛边和塑料感”是后续此界面的持续视觉约束，不把当前观感替用户宣告接受。

Prefab Mode仅局部更换32个同名切片，使用项目既有SpriteDataProvider保留spriteID；纸条轨道高度92U、填充上下22U内边距，原层级、动效/加载C#与Main场景均未改。旧PNG/meta/文案/切片在新批次`preserved/`保存，可恢复对照。一次性作者工具仍拒绝覆盖已有Prefab，换肤工具独立使用`LoadingArt -Isolated`，不重建原UI。

新元素页1024×752，背景576×1024；完整RGBA元素图、不采用会产生色块的动物调色板压缩，背景无抖动256色PNG，相对原背景母图RGB平均绝对误差0.887/255。透明边是从生成白底图处理所得，不是原生alpha。两图1184136bytes（1156.4KiB），比旧版增加922255bytes；RGBA32基础像素参考约5.19MiB，不等于实际GPU或下载量。资源台账仍temporary/first-play、保留原规划超额提示；当前全库登记首批源/计划合计3230891bytes，不能冒充微信2MiB首次下载目标已达标。

局部PlayMode5/5通过后做最终1.22倍颜料对比增强以改善手机细线可见性。最终统一验证运行`20261123T092253436Z-f6e59f87-026e-4440-99cb-07c2cd51de82`，1394项保存输入，指纹`f15c13a3e3f36d911a7d77046474186bd17a8ca39df858baff04447390532d73`：两个隔离副本分别EditMode848/848、PlayMode124/124，均无跳过、退出码0。包含32张切片引用非空/身份保持检查、真实Loading→Main、故障与动画回归。

**统一门禁仍为failed，不称当前整工程通过**：PlayMode结束复查时，并行救助站UI任务新增/修改其脚本和Prefab，报`Source project: input files differ from the saved source manifest`；summary原样保留，不改报告消除失败。本任务33项申报Unity文件与已验快照逐字节一致，范围指纹`deb466ad61f2f15ff57d773ef4d8e6ced232e0e5bc00c1e9805646e2f97fe555`，见[比对](../test-results/loading-comfy/source-comparison.json)。验证内源码451脚本/37程序集0问题，17:40现场新增到454脚本时，救助站进行中StationRoomTests一条行宽问题，不跨范围清理，也不冒充现场源码全通过。

最终实际PlayMode副本为`isolated-PlayMode-788a3593fceb4c748ad1a461f4604ccb`，其`LoadingEvidence/`保存6种比例18张截图、48帧动效。已人工查看360×640/390×845/768×1024画面与深浅底透明边，保持完整轮廓、无粗贴纸边和高光槽；片段GIF与同尺寸前后对比在`test-results/loading-comfy/loading-preview.gif`、`before-after.png`。完整逐图SHA-256在`render-provenance.json`。新版本不引用下面首轮旧版的通过数字，菜单入口和动效速度保持不变。

图片台账检查17:40退出0，本批无缺失/漏登记；超预算与未批准交付提示保留。未提交推送，来源许可、微信/AOT/真机、正式首启下载仍未验；当前只交付临时重绘版，等待用户观感反馈。

以下拆图方法、255.7KiB体积与841/843/123测试是首轮历史事实，不代表当前重绘素材；最终状态以本节和[当前任务](协作任务/20261123-Loading场景-01a20769.md)为准。

## 入口与编辑

- 构建首场景：`Assets/Scenes/SC_Loading.unity`，异步加载第二场景`Assets/Scenes/Main.unity`；Main仍可直接打开试玩。
- Unity菜单：`Homeless Pet Helper > Loading > Open Loading Scene` 打开正常加载入口；`Preview Animation` 仅本次Editor播放停留在Loading观察动效，不写正式场景、不增加生产等待时间。
- 静态UI：`Assets/Prefabs/Panels/Panel_Loading.prefab`。以后在Prefab Mode局部编辑；一次性作者工具遇到已有Prefab会拒绝覆盖，不能重跑生成工具覆盖设计者绑定。
- 房子不转；外围13个节点环绕旋转，每秒14度并各自错峰轻摇。原图两只重叠兔子是一组，避免伪造被遮挡的身体。门边两只独立摇晃，爱心/星星/小花轻浮动和缩放，文字、条框和猫头像稳定。
- 动效使用unscaled时间，组件关闭复位；使用三角函数而非序列帧，不引入新动效框架。Loading单独场景和轻量依赖，离开后不使用DontDestroyOnLoad持有图片。

## 画面与装载

原图只有261×451，按原像素拆32切片；透明边与背景采样修补方法见[资源批次](../art-source/loading-20261123/README.md)，没有生图替换动物、没有宣称高清。受源图分辨率限制，放大细线会有柔化，正式高清素材需要后续另行确认。

1080×1920，CanvasScaler为Match Width；纸纹背景满屏，内容使用既有SafeAreaView，插画等比FitInParent。转圈的完整尺寸与底部进度分区，不整体拉伸动物；无交互热区、费用或手牌。加载文字48U，百分比42U，沿用现有授权字体，文案在独立LoadingText.json。

进度来自Unity的实际AsyncOperation，按0–0.9装载区间映射；资源准备完成显示100%后激活Main，不增加人为倒计时。没有远程下载、地址资源框架或微信进度接入，百分比不能当渠道下载比例。场景缺失/启动异常显示失败并记录异常，不跳过失败报成功。Unity装载不能取消，Owner禁用时停止UI协程并释放激活门闩，防0.9阻塞Unity队列。

构建列表、DesktopBuild及ProjectSetup已局部接入新首场景，Main既有场景/Prefab/玩法/存档未由本任务修改。

## 验证与边界

最终专项：EditMode2/2、PlayMode5/5，覆盖静态绑定/依赖、Loading→Main、真实进度和失败状态、unscaled动效、禁用复位及重开。PlayMode现场保存6种比例18张截图、48帧动效片段；输出目录见下方。6种视口是320×569、360×640、390×780、390×845、360×800、768×1024，另模拟安全区上下和两侧避让，不是手机物理触控/微信挖孔覆盖验收。

- [最终实际截图与逐帧图](../test-results/unity/isolated-PlayMode-883aa068eee148bc83a7df00dfc8a00a/LoadingEvidence/)。
- [Unity动效片段GIF](../test-results/loading/loading-preview.gif)，从48张实际Unity渲染帧派生，4秒截取，不是完整绕圈周期。
- [Loading专项PlayMode](../test-results/unity/PlayMode-isolated-LoadingLocal2.xml)。
- [Loading专项EditMode](../test-results/unity/EditMode-isolated-LoadingLocal1.xml)。
- [本任务34项Unity文件快照比对](../test-results/loading/source-comparison.json)，指纹`4f96b6280a6377fb0d0e8c56b2b0470325109a17fd9f5a63890dade872a11e7e`，与最终专项隔离副本一致。该指纹仅本任务路径，不冒充全项目统一指纹。

统一全量入口本次因其他进行中任务更新导致上下文过期退出，未进入测试，不称通过。另对固定隔离副本跑全量EditMode：841/843，失败两项为救助站进行中新增`StationScene._room`尚未绑定，未由Loading任务代补或放宽断言；同一副本全量PlayMode123/123通过，退出码0。报告为`test-results/loading/full-edit.xml`和`full-play.xml`。不将局部7项或单一模式通过冒充全工程通过。

`npm run assets:check`已实际执行，Loading三条登记与路径有效；同机其他制作中的图还有未登记提示，最终全库报告须由对应任务补齐。Loading两运行图共261881bytes（255.7KiB），透明页超128KiB规划29.0KiB，保留原像素和边缘、不提高预算；叠加原资源后首批源规划仍超额，不代表微信首次下载量。最后基础源码检查450脚本/37程序集0问题；早期其他会话三份新脚本缺meta现已由其流程补齐，不冒用早期检查。来源许可、微信转换/AOT、设备内存、真机首启和最终视觉接受尚未验收，维持temporary/first-play。未提交推送。

复杂度复核：Loading运行时职责分为装载Owner、纯展示、轻动效三个组件；Editor作者工具与渲染证据函数超过40有效行，保留相邻静态装配/捕获与finally清理以便审计，不抽通用UI或截图框架。运行时没有逐帧查找、布局重建、I/O或新建UI；数字仅百分比改变时更新字符串。此为源码复核，不是真机0GC/帧率结论。
