# 动物动作 Unity 接入与资源评估

2026-11-19。用户授权“先把这些已有的加进 Unity 工程，然后分析资源大小”。本页为该批次接续入口，替代此前 HTML 记录中的“未接 Unity”；不是正式美术或跨姿态过渡验收。

## 2026-11-20 运行时迁移

优化后的图集/材质/mesh/clip/Shader/Widget已保留GUID迁到`Assets/Art/PetMotion/`；场景、文本和测试夹具仍留EditorScenes。Main的逆塔防动物位引用该rig，经透明RenderTexture显示；rig位于场景根节点，只在该面板启用且战斗运行时播放。护理仍用原Sprite/Mask配对，不假装已解决动画表面清洁。

六动作、裁边/共享/曲线结果保持；旧独立动作预览保留。MainFinal全量图形EditMode522/522、PlayMode40/40（含真实动物RT像素与旧A/B），详见[Main记录](Main正式流程与资源迁移.md)。下方“优化不接Main”仅表示优化当轮范围；不是当前运行入口状态。图集补边/指纹后续任务已获协调移交，其改动须另验，不能沿用此快照通过数。临时美术、无跨姿态过渡和微信未验边界不变。

## 2026-11-20 优化检查点（当前）

用户确认优先实施保留坐标裁边/UV、共享素材和曲线精简，并要求合图逻辑公共化。三项已完成，**入口及六动作不变**，不接 Main、护理遮罩、正式状态/存档或跨姿态过渡，不生成新图/安装框架/提交推送。下方 2026-11-19 的八整图/原曲线数据是优化前基线，不再代表当前依赖。

### 当前实现

- `tools/pet-motion-atlas.mjs` 从八张原图的非零 alpha 包围盒保留 2px 透明边，不缩放/旋转/重画；`atlas.json` 保存原 452×512 尺寸、裁边 offset、图集区域及原 SHA256。保留区 RGBA 逐字节复制，包括透明 RGB；合成不能直接做多层 alpha-over，否则半透明 RGB 可能被舍入。
- 七个蒙皮保留每个剩余顶点的原坐标、原四权重、骨点和 bind pose；只移除完全在裁区外的三角形和不再被引用的顶点，重映射 UV，不切割并重新插值权重。边界三角形仍使用原顶点，第二套局部 UV 让 Shader 丢弃区域外采样，避免碰到邻图；不在每帧重新算 UV。
- 共享一张 **876×924** NPOT RGBA32 图集、一个材质；关闭 Read/Write/Mipmap，Max Size1024、Clamp/Bilinear，不更改色空间或新增平台压缩覆盖。带 padding 包围矩形占页 98.1%，不是非透明像素占比。七蒙皮及三原图对照共用材质，坐/卧对照也复用相应静态 mesh；站立对照另有裁边 mesh，不能再套 body 的 UV。
- 原八张 public PNG 与 Unity 原始拷贝均未改。原拷贝/旧材质保留作引用，但不在当前 rig 递归依赖中。远近腿存在阴影和腿根 alpha 差异，因此共图集而不强行共用同一像素区域。同种实例共享 mesh/material/texture/clip 引用，骨骼与播放状态独立，不按动物复制资产；**五层蒙皮仍不是一次 draw**。
- 仍为原生 `.anim`，保留时长、60Hz源采样、阶跃镜像、向量恒定兄弟轴及末尾0.15秒同骨架闭合。移除没有蒙皮权重/受影响后代的离线 CCD target 曲线；线性简化容差改为位移0.05源坐标单位、旋转0.08度、缩放0.0002。先做保守有界压缩，没有重写 IK/短周期/程序微动，运行时模块无改动。
- `PetMotionBake` 更新六曲线、保持 GUID 的 mesh，并通过 Prefab Mode 修改已有 Widget 的绑定，不重建面板或场景；骨点/静态姿态不同会拒绝 UV-only 升级，必须另做骨架迁移。最终重烘焙同时验证已有优化资产的可重复升级。

### 公共图集工具

复核已有实现：旧 `tools/pet-rig-process.mjs` 是固定 768px/3×3 分件合图，不是通用紧凑打包；`ImageAtlasAudit.cs` 只检查 Unity SpriteAtlas。现将排布/合图集中在 **`tools/lib/image-atlas.mjs`**，两套离线处理都调用，旧原型保持固定布局/调色板规格且本轮不重生原图。

公共 API 为 `packAtlasRegions(regions, options)` 和 `composeAtlas(sharp, layout, images, pngOptions)`，支持自动紧凑或显式固定布局，检查尺寸/ID/边界/重叠/缺图；不写文件、不承担动物裁边/UV或台账、不做运行时加载。契约和复用方式见 `tools/lib/README.md`，无需再为别的主题复制合图算法。

### 同口径实测

Unity2022.3.62f3 / StandaloneWindows64，包含原图对照。只计算当前 rig 的递归依赖：

| 项目 | 优化前 | 当前 | 减少 |
| --- | ---: | ---: | ---: |
| PNG 依赖 | 8 / 556476 bytes | 1 / 518906 bytes | 源字节6.8% |
| RGBA32 基础像素量 | 7405568 bytes / 7.06 MiB | 3237696 bytes / 3.09 MiB | 56.3% |
| 七个蒙皮总顶点 / 三角形 | 14063 / 26880 | 5525 / 10350 | 60.7% / 61.5% |
| 骨骼动画关键点 | 32424 | 17679 | 45.5% |
| mesh 源文件（当前含额外原图对照mesh） | 1820787 bytes | 1107426 bytes | 39.2% |
| 六 `.anim` 源文件 | 17352669 bytes / 16.55 MiB | 9908280 bytes / 9.45 MiB | 42.9% |
| Editor 纹理对象 | 14816576 bytes | 6476072 bytes | 56.3% |
| Editor mesh / clip对象 | 1799524 / 3205026 bytes | 1084436 / 1860106 bytes | 39.7% / 42.0% |
| rig-only Windows64 LZ4 Bundle | 1517647 bytes / 1.45 MiB | 1148042 bytes / 1.09 MiB | 24.4% |
| rig-only Windows64 LZMA Bundle | 655833 bytes | 526546 bytes | 19.7% |

**不是微信下载/目标GPU内存/整包结论。** 保留原图并新增派生图会增加仓库图片存储；台账现在33张temporary/preview-only、2381.0KiB，包含HTML原图与独立派生图，不能拿这份全库源文件统计冒充 Unity 当前依赖。原坐姿158.6KiB超150KiB计划及首批未确认提醒均保留，新图集按本批八图共同使用规划512KiB，不静默放宽原单图计划。

### 验证与接续

- 图形 PlayMode `PetOptimize3` **10/10，无跳过**：原8项、两实例静态资产共享/骨骼独立、与旧提交rig的A/B；保留6比例36截图和安全区下拉截图，已查看站/走/坐/卧画面。
- A/B 使用优化前提交 `a19ca31` 对应的旧 Windows64 rig-only Bundle，不从优化后重新录基线。六动作各16归一时点全部保留顶点最大差 **0.3396553** 源坐标单位；六动作×原图/动画×深浅底×4时点共96组像素对照，RGB平均绝对差最大0.07025909（0–255），RGB总差>48的像素占比最大0.04082%。镜像姿态按实际蒙皮矩阵核对，避免默认BakeMesh去除镜像scale造成假错。
- Three独立顶点基准扩为六动作×8时点×有效层×5点，误差<0.65；不将恰好落在浮点取模换向边界的离散原采样异常当新回归。旧运动采样/60Hz换向误差未在本优化中修改，A/B仍覆盖两版在边界的等价性，非承诺任意时点逐帧等于Three。
- 预热200次walk CPU采样 **0托管bytes / 2.5007ms**，仅Editor CPU总循环，不是微信全帧Profiler。`npm run test:atlas`7/7（含像素/坐标/权重/骨点/公共工具错误分支）、HTML动作/姿态/旧rig/台账44/44、Vite构建、生成一致性、源码213脚本/22程序集检查通过；保留原Three大chunk提示。未跑本轮全工程玩法回归。
- 证据：`test-results/pet-motion/source-size.json`、`unity-size-optimized.json`、`optimized-contact.png`；`PlayMode-isolated-PetOptimize3.xml/log`，`isolated-PlayMode-0580ee01766846d9b3a8d7fbf107d1fd/PetMotionEvidence/`；`PetMotionAudit-isolated-PetOptimize2.log` 及对应隔离Library。图片检查无缺失/漏登记。大测试方法保留线性笛卡尔积编排，按复杂度触发线复核，不为行数拆无意义转发。

复现顺序（Sharp沿用本机依赖，设置`CODEX_NODE_MODULES`）：

```powershell
node tools/pet-motion-atlas.mjs --check
node tools/pet-motion-unity.mjs --check
npm run test:atlas
powershell.exe -NoProfile -File tools/unity.ps1 -Task PetMotionBake -Isolated -ReportSuffix PetOptimize
powershell.exe -NoProfile -File tools/unity.ps1 -Task PlayMode -Isolated -Graphics -TestFilter HomelessPetHelper.Tests.PetMotion -ReportSuffix PetOptimize
powershell.exe -NoProfile -File tools/unity.ps1 -Task PetMotionAudit -Isolated -Graphics -ReportSuffix PetOptimize
npm run assets:check
```

需重生时去掉两个`--check`，先图集再导出；隔离Bake不会自动覆盖工作区，只能核对后复制本批mesh/clip/Widget/新材质和`.meta`，禁止整份Assets覆盖。A/B命令会将本机`test-results/pet-motion/baseline-rig.bundle`复制到隔离Assets外；它须来自**旧提交的**PetMotionAudit/LZ4输出，缺失会明确跳过A/B而非假报通过。本轮确有旧Bundle，10项全部实际执行。

下一步先收优化版Unity观感反馈；三项不再是待做。短周期/程序微动、移除生产对照依赖、按需姿态和微信纹理压缩仍需独立评估，不自动扩展到生产加载框架/所有品种。微信真机、完整Player/微信包、正式美术许可及跨姿态过渡仍未验证。

## 2026-11-19 导入基线（历史）

## 入口与范围

- Unity 工程 `unity/HomelessPetHelper`，菜单 `Homeless Pet Helper > Preview > Open Animal Motion`，打开后 Play。
- 保存场景 `Assets/EditorScenes/PetMotion/SC_PetMotion.unity`；静态面板 `Assets/Prefabs/Panels/Panel_PetMotion.prefab`，嵌套动物 `Assets/EditorScenes/PetMotion/Widget_PetMotion.prefab`。后续使用 Prefab Mode 编辑，不重新创建覆盖。
- 六个动作：呼吸休息、张望摆尾、原地踏步、来回走动、完整坐姿休息、完整侧卧休息；可暂停、调速、拖动进度、复位、切对应原图。拖动进度会暂停，原图模式冻结动作时间；后台/失焦不累计。
- 幅度固定为 HTML 已验证的 0.8；没有移植旧趴下/旧网格坐姿，没有跨姿态过渡、排队、过渡生图或生产状态机。动作下拉当前为直接切换，不声称已解决站坐卧连续过渡。
- 保留全部 HTML 入口、原 PNG、同期地图/站内/逆塔防工作及存档；不接 Main、正式救助或站内状态。仍为 temporary / preview-only，画风、身份差异、毛边、商业许可与微信验收待处理。未安装动画框架或新包，未提交推送。

## 实现与生成源

`tools/pet-motion-unity.mjs` 直接复用 `src/pet-mesh/skin.js` 与 `src/pet-layers/` 的当前网格、四权重、骨架、采样和官方 Three CCDIKSolver，离线输出原生 Unity 所需数据。不是重新拆图、生成独立部件、序列帧或播放网页。

- 原来的 7 个蒙皮层各 2009 顶点 / 3840 三角形；站立 5 层依次远后腿、远前腿、身体、近后腿、近前腿；坐/卧各为单个连续整图网格。Unity `SkinnedMeshRenderer` 使用相同顶点、UV、bind pose 与最多四权重。
- 60 Hz 离线骨骼采样，去除未动画的属性族，保留动画向量的恒定兄弟轴，线性简化容差：位移 0.03 源坐标单位、角度 0.03 度、缩放 0.0001。保留位置的 X/Z 很重要：Unity 的部分轴曲线会将未写轴采为默认值，不能只写坐姿胸部 Y。
- 6 条原生 `.anim`，不在运行时求 IK。镜像使用阶跃而非穿过零缩放。呼吸/张望取 20π 秒、踏步 22 秒、走动 10.44 秒、坐姿 6.4 秒、侧卧 7 秒；每段最后 0.15 秒只对同一骨架闭合循环。这是预览循环适配，不是跨姿态过渡，也不承诺整段与 HTML 无误差逐帧一致。
- `art-source/pet-motion/unity-export.json` 为可重建的离线生成数据，留在 Assets 外。`PetMotionFixtures.json` 是从 Three 计算的测试顶点，未被预览 Prefab 引用。生成文件从脚本更新，不手改 `.anim` 或 JSON。
- 本机 Node 24 在此长批导出中出现过 JIT 下时长元数据不稳定；导出工具自动使用 `--jitless`，并检查时长、曲线末点和重复生成一致性。只影响离线制作，不影响 Unity 运行时。
- `Modules/PetMotion` 只负责动画预览、控件与渲染面；`Editor/PetMotion` 负责一次性资产制作及资源评估。运行时没有查名字或拼 UI。显示使用独立 Camera/RenderTexture，后者由 `PetMotionSurface` 持有，关闭/销毁释放，暂停无变化时不重绘。

所有 8 张导入 PNG 与 HTML 原文件逐字节一致，未裁边/重压/放大。唯一台账将 Unity 路径设为当前 runtime，原 public 文件保留为 reference 并继续供 HTML 使用；只计一个资源归属，未把临时图改为正式首批。

## 图片大小实测

8 张均为 452×512，默认 Windows 导入 RGBA32、Max Size 512、NPOT None、Read/Write 关闭、Mipmap 关闭、Clamp/Bilinear，无 WebGL 专用压缩覆盖。

| 图 | PNG 源 KiB | RGBA32 基础 KiB | 非零 alpha 包围盒 |
| --- | ---: | ---: | --- |
| 站立原图，仅对照 | 74.5 | 904 | 433×487 |
| 身体 | 110.1 | 904 | 433×482 |
| 近前腿 | 17.9 | 904 | 88×175 |
| 近后腿 | 22.9 | 904 | 112×179 |
| 远前腿 | 17.6 | 904 | 88×178 |
| 远后腿 | 22.9 | 904 | 112×182 |
| 完整坐姿 | 158.6 | 904 | 430×434 |
| 完整侧卧 | 119.0 | 904 | 410×239 |
| 合计 | **543.4** | **7232（7.06 MiB）** | 仅分析，未裁切 |

源图总计 **556476 bytes**，基础 RGBA32 **7405568 bytes**，不是同一指标。去掉仅原图对照需要的站立原图，生产候选仍有 7 张 / 469.0 KiB 源图 / 6.18 MiB 基础纹理；坐/卧原图对照复用对应纹理，不另外复制。

四张腿各只有约 5.1%～6.7% 像素带 alpha，却分别保留整张 904 KiB 基础纹理。四腿合计 3.53 MiB；按非零 alpha 包围盒的理想裁边仅约 0.273 MiB，约少 92%。全 8 图理想裁边合计 **3102732 bytes / 2.96 MiB**，比 7.06 MiB 少约 58%；这是无 padding、无压缩块对齐、无图集空隙的面积参考，不是已实现的交付节省。

坐姿仍超过原单图 150 KiB 计划，保留 `ImageOverPlan`，没有静默涨预算或为达标损坏图片。源文件台账全库仍是 32 张临时图 / 1874.3 KiB，因导入拷贝而改变路径但不重复计数。

## 不只看 PNG

资源评估命令导出 Windows 当前导入格式、源文件字节、Editor 已加载对象内存、网格/动画源文件和原生 AssetBundle 对照；报告明确不冒充微信包或显存。

本轮图形审计，Unity 2022.3.62f3 / StandaloneWindows64：

| 实测项 | Bytes | 含义 |
| --- | ---: | --- |
| 纹理 Editor 已加载对象 | 14816576 | 约 14.13 MiB，Editor 对象统计，不等于目标 GPU 显存 |
| 7 个网格源 `.asset` | 1820787 | 文本/序列化源文件 |
| 6 条动画源 `.anim` | 17352669 | 约 16.55 MiB 的 Unity 文本序列化，不等于交付包 |
| 网格 Editor 已加载对象 | 1799524 | 约 1.72 MiB，当前保留可读网格 |
| 动画 Editor 已加载对象 | 3205026 | 约 3.06 MiB，不是目标帧耗时 |
| rig-only Windows64 Bundle，LZ4 | 1517647 | **约 1.45 MiB**，含动物 Prefab/纹理/网格/动画/材质/Shader 依赖 |
| 同一 rig-only Bundle，LZMA | 655833 | **约 640.5 KiB**，更高压缩不等于更适合实时加载 |

Bundle 不含 Unity 引擎/业务代码/UI 面板/字体，不是完整 Player、微信转换输出或网络请求；只是本批同一组原生资产的两个压缩对照。透明纹理在磁盘压缩中可很小，但解码后仍占完整像素量。无图形的早期审计纹理对象仅约 7.07 MiB，不能拿该值替代上述图形实测或目标 GPU 测量。

- 网格总计 14063 顶点 / 26880 三角形，站立同时显示 10045 顶点 / 19200 三角形，坐/卧只显示一层。当前规则网格也覆盖透明区域，仍有裁网格与降低密度空间。
- `.anim` 为 Unity 文本序列化，长循环和大量骨骼关键点导致仓库文件较大；YAML 字节不能直接当作 Player 包大小。它们不按动物个体复制，同一外观模板应共享网格、材质、纹理和动画，仅持有独立骨骼/播放状态。
- 本预览为了方便对照会同时引用全部 8 张纹理和 6 条动画。生产不应把全部品种、全部姿态放进启动 Prefab 的直接引用；当前未安装资源加载框架或声称已具备按需下载。
- 预览专用 RenderTexture 长边固定上限 768、无深度/Mipmap/MSAA，基础颜色缓冲最多 2.25 MiB，实际随舞台宽高比变化。它不是每只动物额外一套贴图的设计，也不建议正式站内每只动物一个独立 RT。
- 预览纹理不在 Main 递归依赖或预加载中，测试场景不在 Build Settings；无 Resources/StreamingAssets 新增。仍须在最终微信构建/转换后核实产物与请求，不能仅凭目录宣称发布包没有任何影响。

## 优化顺序

1. **先裁透明留白并保留原坐标系**：制作派生图与 `(offset, originalSize)` 元数据，重映射 UV/几何裁边；骨架/脚底/清洁遮罩继续使用原 452×512 坐标。不要直接覆盖原图，否则四腿、点击区和挂点可能错位。优先四腿，其次侧卧上方透明区。
2. **共享模板，不按个体复制资源**：同种同规格复用一套资源；名称、状态与护理脏污走数据/遮罩，不为五只陪伴动物复制五套纹理。已复制的远腿还可另试复用近腿纹理、坐标映射与材质阴影，须比对关节/明暗再替换，不能直接改掉已认可效果。品种轮廓不同再评估独立模板，不能保证仅换色就能套所有骨架。
3. **把重复微动转成少量参数，减少长段烘焙曲线**：呼吸/头耳尾可后续做同骨架程序采样，步态使用短周期骨骼片段；不要将 60 多秒微动、原图对照与测试数据作为正式每个外观的必需数据。本轮先保持可比对，未重写已认可运动。
4. **按真实共同使用时机评估裁边图集和网格密度**：图集不会自动裁好自定义蒙皮 UV，也不会自动让 5 个 SkinnedMeshRenderer 合成一次 draw；保留边缘采样余量，不为几个部件盲开 2048² 页。进一步比较三角形数/线条误差和 GPU 成本。
5. **最后做目标纹理压缩与按需加载**：8 张未裁图若使用 ASTC 4×4 / ETC2 RGBA8，块数据理论约 1.77 MiB；ASTC 6×6 约 0.80 MiB。只是格式估算，不是已验证微信配置或下载量；设备回退仍可能回到 RGBA32。细线/毛边/白毛/色带需同尺寸 A/B 和微信真机验证。

## 重跑与验证

```powershell
node tools/pet-motion-unity.mjs --check
# 新资源仅首次创建：-Task PetMotionAssets -Isolated；现有 Prefab 不重建。
powershell.exe -NoProfile -File tools/unity.ps1 -Task PetMotionBake -Isolated -ReportSuffix PetMotion
powershell.exe -NoProfile -File tools/unity.ps1 -Task PlayMode -Isolated -Graphics -TestFilter HomelessPetHelper.Tests.PetMotion -ReportSuffix PetMotion
powershell.exe -NoProfile -File tools/unity.ps1 -Task PetMotionAudit -Isolated -Graphics -ReportSuffix PetMotion
npm run assets:check
node tools/check-unity.mjs
```

`PetMotionBake` 只在隔离副本更新原生曲线并在 Prefab Mode 校正该新面板；不会自动覆盖工作区。需要复制经过核验的本批 `.anim`/面板及保持原 GUID，不能复制整个隔离 Assets 覆盖同期改动。`PetMotionAudit` 在隔离 Library 下输出 `PetMotionAudit/pet-motion.json`，图形运行还生成 LZ4/LZMA 的 rig-only Windows64 对照 Bundle；它不是只读全库审计，也没有配置正式 Bundle 名或发布分包。

当前 Bake 不更新骨架结构、网格或权重；未来修改这些源定义时需专门迁移 Mesh/bind pose/绑定，再跑 Three 顶点对照，不能只重烘焙曲线。当前已启用的 Animation 内置模块来自现有依赖；后续清理 Editor 插件时也要核对运行时动画模块，不因它当前经工具依赖启用而误删。

`tools/pet-motion-size.mjs` 读取 PNG/alpha 并核验 Unity 拷贝哈希，仅写忽略的 `test-results/pet-motion/source-size.json`。需要本机已提供的 sharp，通过 `CODEX_NODE_MODULES` 指向桌面依赖目录，不必安装新依赖。

### 本轮结果

- 最终图形 PlayMode `PetMotion6`：**8/8**，含导入/依赖、Three 顶点对照（6 动作×2 时点×有效层×5 点，误差小于 0.65 源坐标单位）、切换复位、真实控件、关闭释放/重开、6 宽高比共 36 张截图/非空猫像素与踏步前后像素变化。另有 390×844 顶部46/底部34像素安全区、实际下拉展开/6 项至少88设计像素行高/选择侧卧及截图，共37张。坐姿/侧卧手机和平板图、完整下拉已人工查看，不再把空白渲染或裁切的小行当通过。
- 预热后 200 次 walk CPU 采样：托管分配 0 bytes / 2.1992 ms，总循环测量。不是全帧 GC、完整渲染性能或微信真机结论。
- 图形 `PetMotionAudit4` 成功生成两个原生对照 Bundle；原图 8 份字节/哈希一致；源检查 211 C# / 22 程序集、issues 为空；HTML 动作/姿态与台账回归 40/40；台账无缺失/漏登记，保留坐姿超计划/首批未确认两项提醒；导出重复生成校验与 `git diff --check` 通过。
- 证据：`test-results/pet-motion/source-size.json`、`unity-size.json`；`test-results/unity/PlayMode-isolated-PetMotion6.xml/log`；`isolated-PlayMode-858464d7c25b4f1c9cc36f8d1c841a67/PetMotionEvidence/`；`PetMotionAudit-isolated-PetMotion4.log` 与相应隔离 `Library/PetMotionAudit/`。最终第5/6轮只改面板下拉行高和样式，动物资源与第4轮大小审计一致。隔离制作/验证不会切换用户当前编辑器场景或覆盖其未保存内容。
- 一次性 Editor 资产制作与测试编排有超过 40 有效行的方法；按复杂度规则复核后保留线性结构装配，网格/曲线/控件/审计已分别归属，没有为凑行数机械拆转发层。运行时类型职责单一，热路径无查找、IO 或分配式采样表。

微信/真机、Player 实际发布包、全帧 Profiler、生产状态转换、最长语言和正式许可仍未验证；本次不是全工程玩法回归或发版验收。
