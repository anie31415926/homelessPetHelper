# 图集工具补齐

- 任务 ID：20261120-图集补齐-atlas-tooling。
- 负责人：Codex `01a1ed54-d51a-76c1-80e3-a5cf24f9cd87`，工作标识 `atlas-tooling`。
- 最近更新：2026-11-20T16:25:00+08:00。
- 状态：完成。
- 用户授权：补齐图集审查的四项问题：固定 Sharp 依赖、防串色补边、普通 UI 原生 SpriteAtlas 流程、构建前一致性检查。

2026-11-20T15:33:00+08:00 接续授权：用户要求结合实际使用和学习案例评估合图，选择当前最合适的方案。已从磁盘重读根AGENTS、当前协作状态、任务README及所有未结束任务，并读取协作同步通知；此前四项实现不重复做。认领后复读工程执行记录任务，其占用在协作入口与独立协作工具，不重叠以下本轮路径。

本轮重新认领如下结构化范围；评估临时图集仅写隔离项目，报告放Library/AtlasAudit或test-results。不改package、协作工具/入口、Main/Prefab、原图/Importer、动物生成链、SDK与玩法。接续复读发现猫狗首批任务新增且持有resource-catalog，本轮撤出该文件认领并保留其内容，未写入。没有新图入库，先前台账所记1024²/4MiB保留为初版实测，当前停用决策与配置以图集记录为准。

```collaboration
{
  "sessionId": "01a1ed54-d51a-76c1-80e3-a5cf24f9cd87",
  "writePaths": [
    "tools/image-size-preview.mjs",
    "tests/image-size-preview.test.mjs",
    "docs/图片缩图开源参考与实测.md",
    "test-results/image-size-review/",
    "docs/协作任务/20261120-图集补齐-atlas-tooling.md",
    "docs/协作执行记录/20261120-图集补齐-atlas-tooling/"
  ]
}
```

本轮工具接入前的接续修改按真实时间记录，不补签先前已读。工具可用后补齐结构化范围，07:37:51Z首次scope-change记录，后续因其他任务范围变化重新读取并追加resume，最后07:47:51Z检查通过。其他未结束任务范围已核对，除已撤出的台账无重叠；交付按新上下文追加delivery，不改旧JSON。

## 首轮写入范围（历史）

2026-11-20T16:14:00+08:00 接续：用户要求在 GitHub 查找开源参考，小图可缩小但不明显变糊。已重读根 AGENTS、协作入口、任务模板、同步任务与首批玩法最终交付；首批玩法已于16:09释放路径。仅认领上方独立缩图评估工具、测试、报告及忽略目录预览，不认领原图、Importer、台账、Prefab、图集生成链或共享入口。本轮用已有 Sharp，不安装新框架，不将候选图替换为正式资源。图集上一轮决策及释放记录保留，接续不会重做合图。

2026-11-20T16:22:00+08:00 复核：缩图工具和5项测试已通过，8张图三档同尺寸深浅背景对比已查看，评估原图哈希未变。Sharp/oxipng/Squoosh和Unity官方Addressables样例已读取真实README/部分源码；不采用自动全库压到128。检查发现新增基础存档与按住/图片估算任务导致旧回执失效，已读取二者范围，无工具/报告路径冲突；玩法和Packages的同期变更保留，下一步写独立研究结论。台账及图集只读检查通过，不把它们称为微信包体测量。

- 独立文件：`tools/lib/atlas-padding.mjs`、`tools/lib/atlas-manifest.mjs`、`tools/check-atlases.mjs`、`tools/atlas-unity.ps1`、相关新增 Node 测试；`Assets/Scripts/Editor/Atlas/`、`Assets/Tests/EditMode/Atlas*` 与 `.meta`；`Assets/Settings/Atlas/`、`Assets/Art/Atlases/` 新资产；`docs/图集工具与验证.md`。
- 已协调可写：`package.json` / `package-lock.json`；`tools/lib/image-atlas.mjs` / README、`tools/pet-motion-atlas.mjs` / `pet-motion-unity.mjs`、相关现有图集测试；`art-source/pet-motion/atlas.json` / `unity-export.json` / 台账；`Assets/Art/PetMotion/` 派生图、网格、材质、Shader 及必要 Editor 烘焙集成。
- 构建门禁使用独立 `IPreprocessBuildWithReport`，优先不编辑同期 `DesktopBuild.cs` / `tools/unity.ps1`。

## 不负责与依赖

首轮获用户授权并向Main协调；对方于14:59:52+08:00释放图集范围、15:03:07+08:00释放其余范围。本任务始终未改tools/unity.ps1、DesktopBuild、Main、面板或Game/Bootstrap/Siege UI；不把该历史占用当现时锁。当前按上方结构化范围执行，猫狗首批新增工作不接管。
不生图、不改变玩法、不接资源下载框架/CDN、不改正式美术或发布状态、不提交/推送。

## 当前事实与下一步

四项实现已齐：本地 Sharp 固定0.35.5、完整透明补边、显式UI SpriteAtlas组及菜单/隔离命令、35源/生成文件+8实际网格/UV/绑定的独立构建门禁。现有动物图集仍876×924/518906bytes，像素/UV未变，没有重烘或覆盖现有rig。普通UI原生试打包Windows/WebGL各一页1024²，保留Sprite和Atlas GUID；内存4MiB比散图1.90MiB高，已明确记录，不称性能优化通过。

本轮选择已落地：动物蒙皮保持既有实测收益；地图UI选散图。实际地图仅四建筑Image，四动物候选在地图没有Image绑定，Main只另引用橘猫。原尺寸native对比：八图1024页4MiB，512上限四页2.75MiB，仅建筑两页1.5MiB，而建筑散图0.903MiB；当前Main候选五图散图共1.153MiB。结合Unity官方属性/UI优化/Scenario2，不启用无收益证据的大图集。
停用组保留配置候选和atlas GUID，但关闭include、清空packables和全部旧packed缓存；门禁拒绝停用残留缓存。新增Evaluate、BuildProbe隔离命令，验证真实构建像素负载，不能只关闭include导致源纹理被剔除。

## 验证与交付

本轮Node14/14、台账13/13、生成检查35文件/8网格、229脚本/23程序集0issues及Vite构建通过；独立干净目录npm ci/本地Sharp原生PNG编码通过。根目录npm ci遇已有文件锁，已恢复依赖且重验，不终止服务/不强删缓存。
全量图形EditMode531/531后仅补alpha/format导入门禁，最新专项9/9；PlayMode最终完整快照40/40无跳过，动物A/B与Main三尺寸证据已重跑，地图两尺寸已查看。真实BuildPipeline Development构建通过，已触发图集门禁/原生打包；本机64位Mono运行库缺失未解决，不称原生启动通过。各命令/快照/证据和未验边界见独立[图集工具记录](../图集工具与验证.md)。

上述为首轮快照，不覆盖同期变化。本轮最新图集EditMode10/10（Atlas-CleanDecision.xml），Main PlayMode3/3（PlayMode-AtlasStandalone-Final.xml），无跳过；原生Windows/WebGL四候选对比通过。干净Development实际构建及像素负载探针通过，94099775bytes；四建筑+橘猫Texture2D有完整负载，停用atlas未入包。初次旧缓存导致负载缺失、一次增量报告省略复用资产的探针失败均已定位并修正，未吞错。
本轮图集Node14/14、atlas:check35文件/8网格通过；最终静态检查235脚本/23程序集0issues，台账无缺失/漏登记。未重跑全量当前玩法、未测drawcall/帧时或真机；源码快照、报告、官方参考见工具记录，不宣称全局永久最优。没有提交/推送。

## 占用与交接

2026-11-20T15:24:00+08:00 释放本任务上述全部路径；未提交/推送，保留其余会话的未提交修改。未改 tools/unity.ps1、DesktopBuild、Main场景、面板或Game/Bootstrap/Siege UI。未来改动物图/生成逻辑须同时重生并校验35文件清单和实际网格；UI组内存增加约2.10MiB须目标机评估，不升级临时美术审核；微信仍待真实AppID、转换、下载及真机验证。

2026-11-20T15:53:00+08:00 本轮实际使用评估与选择完成，释放上方结构化范围的全部写入占用，仅按流程追加本任务delivery记录。后续资源增长/正式图/压缩与渠道变化时重新评估；UI候选保持停用，不混同动物蒙皮图集。保留其他任务全部改动，台账由猫狗首批任务持有，本轮未写。微信、原生Mono缺件、真机批次/帧时验收仍未完成。

2026-11-20T16:25:00+08:00 缩图开源调研与候选实测完成，释放本轮结构化范围。交付独立工具/5项新测试及[开源参考与实测](../图片缩图开源参考与实测.md)，8原图哈希/工具哈希与机器报告一致；缩图+既有图集Node共19/19通过，atlas:check35文件8网格一致、台账无错误/缺失/漏登记。新工具SHA256 `775b9900185bd5bac22c83e36ea11dc7421f71bd9d820922f4bda93671c3580a`，测试SHA256 `55f889b18a37bfb3a04eb37c0aef4faa0330225a870e17ab6d7911d0c4eda81d`；不是全仓指纹。报告及5张比较图在test-results/image-size-review，可重生，不入正式台账。
选择：真正最大显示≤128的小图采用128候选；战场共用猫狗保留256，建筑192是待最高尺寸核对的候选，不统一降128。四动物256→128普通PNG条件化合计210.2→68.9KiB、RGBA32基础像素少75%，不是已减少实际包体。已查看同尺寸深浅背景、224战场放大与建筑调色板候选；未覆盖原图/Importer/Prefab，未安装新依赖、未跑本轮Unity/微信/真机，未提交推送。并发存档/按住任务原范围不动，其后续实现不在本轮测试结论内。原始测试与后续替换条件详见报告；交付按当前上下文追加delivery，不改历史回执。
