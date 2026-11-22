# 林缘美术 Unity 接入

2026-11-21。用户认可 ComfyUI 候选并明确“可以接入到工程里”。本轮将认可图片接入已保存的 Main 流程，不增加求助供给、森林切换或现场新规则。任务与全部验证证据见[协作任务](协作任务/20261121-林缘美术接入-01a1f74d.md)。

## 当前接入

- 地图采用768×1024地面和7处独立透明地标，六处地点均有独立按钮、名称及求助状态；救助站只作展示。保留原地面、4地标及3面板的路径和GUID，新增资源的meta由Unity生成。
- 配置地点为单车棚、居民小院、临街门廊、林缘公园、市场后院、河岸步道。保留原三条求助；新三处没有人为补造求助，在Main可看详情但沿用已有探索未开放限制。
- 6张现场背景由`GameContent.asset > Outing Artwork`按稳定地点ID显式绑定。Main开路及逆塔防每次进入时读取当前地点图片；不按猫狗种类决定场景，不从节点名/数组顺序推断地点。
- 图片和操作层分离。开路背景不拦截指针，地面/可达格透明度降低，墙、水沟、箱子及杂物保持原规则和可读层；逆塔防背景位于战场Graphic之后。按钮、棋盘障碍和路线仍是原白板试做表现，本轮未绘制这些交互资源。
- 森林承接图已导入`Assets/Art/CityMap/T_CityMap_Forest.jpg`供后续编辑；Main不引用，没有增加切图入口、解锁条件或无缝连接。整体合成图只在Unity外供审核，避免与独立地标重复叠加。
- 新玩家文案在`CityMapText.json`，字体依既有脚本重建为680个cmap条目、171076字节，字体GUID与OFL保留。

路径相对`unity/HomelessPetHelper/`。完整来源、运行路径、尺寸与SHA256见[15项导入清单](../art-source/forest-edge-20261121/unity-import.json)，生成依据/母图仍在[出图批次](../art-source/forest-edge-20261121/README.md)。图片只记temporary；用户观感认可记visual accepted，许可/目标仍pending，不视为发布批准。

## 编辑与扩展

日常在Prefab Mode编辑三个已保存面板。地图按源布局比例锚定地标，名称牌独立于插画；Point Bindings继续绑定稳定ID。新增地点同时补配置、文案、静态点位绑定和`Outing Artwork`，加载校验拒绝空图、重复/未知ID及缺失地点；具体流程见[地图配置与扩展](地图配置与扩展.md)。

`ForestEdgeArtImport.Apply`是本批增量导入/Prefab编辑工具，只允许在带`ForestEdgeSource`的batch隔离副本执行；入口`tools/unity.ps1 -Task ForestEdgeAssets -Isolated -Graphics`。它保存Prefab内容而不重新创建面板。已完成接入后不要对用户后续编辑的Prefab重跑本批导入或旧面板创建器，以免重置人工图片/位置/绑定。

导入采用Sprite Single、100PPU、Clamp/Bilinear、不开Read/Write或Mipmap，不缩放NPOT；透明地标max512，背景max1024。选用Uncompressed保留NPOT透明轮廓，非微信压缩方案。图像保留比例，现场插画和棋盘比例不同处留白，不把示意路线解释为美术中的真实道路。

## 资源成本和边界

唯一台账为[resource-catalog.json](../art-source/resource-catalog.json)。本批15项中，地面+7地标+6现场共14项实际静态Main依赖，全部按first-play记账；即使某地点暂不能出发，静态绑定仍会成为依赖，未声称已按需加载。森林不在Main依赖中，记preview-only；被替换的5条旧地图资源记retired，源资料继续追溯。

Unity只读审计确认当前Main静态依赖19张图片，源文件合计1,611,138 bytes（1573.4KiB），比1536KiB源文件规划线多37.4KiB。两张原三花猫/兔角标没有Main引用，图集IncludeInBuild=false且packables为空，已据此转preview-only；没有仅靠改标记压低统计。CityMap389.2KiB、Rescue426.3KiB、Station757.9KiB，仍在各模块源文件复核线内。台账无缺失/漏登记，保留22条review提醒：范围草稿、总量超规划、旧坐姿超单图规划及19条未获最终交付批准。没有提高预算或把静态依赖标成延后加载。

只读审计记录本批14张Main依赖源图721,614 bytes，RGBA32参考合计22,952,944 bytes（约21.89MiB）；全部19张Main图片RGBA32参考28,812,080 bytes（约27.48MiB）。当前Editor背景RGB24、地标RGBA32，参考值不是实际驻留内存。审计报告`test-results/unity/ForestEdge-resource-audit.json/.md`，对应只读隔离副本；导入设置与本轮保存资源一致。

源文件字节不是Unity纹理内存、转换后包体或微信首次下载；2MiB微信首次图片下载目标仍未实测。后续资源优化应先测量构建/设备，并据实际需求决定压缩和按需加载，不因本轮接入推定目标达标。

## 试玩和验证

打开`Assets/Scenes/Main.unity`，站内外出进入六点地图。选单车棚或居民小院，经现有随机开路完成真实通路后进入逆塔防，观察两阶段使用同一地点背景；提前回站后再去另一地点，背景应切换且不残留上一处。新三点应显示中性详情和暂无求助，主按钮沿用探索未开放提示。临街门廊仍为原未接入Main的寻宠示例。

本轮定向PlayMode 2/2通过，包含地图6种比例和模拟安全区、地标本体及名称牌射线、实际字体网格、连续两次外出地点切换、暂停恢复和无求助点选副作用。修复了名称牌行间重叠、名称文字空间不足与救助站标签被底部详情遮挡。基础源码检查344脚本/35程序集/0问题。已核对15份导入图片SHA256一致、8个既有资源/Prefab GUID保持、8份不相关既有脏文档逐字节保持。

统一工程验证入口最终通过：EditMode **766/766**、PlayMode **74/74**，两个Unity进程与验证入口均退出0，无跳过/未决测试；基础源码检查344脚本/35程序集/0问题。包含原Main猫狗双救助、开路/战斗暂停与提前返回、回站护理、容量扩展及存档恢复的全量回归。

运行ID `20261121T134903642Z-e680982d-365c-4749-9119-ea4bc2f329c9`，证据目录`test-results/unity/verify-20261121T134903642Z-e680982d-365c-4749-9119-ea4bc2f329c9/`，其中`summary.json`状态passed。1108项保存输入与两隔离副本逐项一致，输入指纹`6280d80a2c8a37f5466a75ca068077485556690d3833c1acdabe4d3fc9f167ff`，清单`inputs.json`。本轮Unity只读审计的34份图片/meta/Prefab/GameContent资产与保存源文件字节一致。

首轮全量EditMode曾765/766，旧透明占比>20%假设不适用于新救助站轮廓；逐图核对alpha后改为四角透明、主体不透明和柔边，定向14/14与最终全量均通过，原失败证据保留。没有修改图片或降低导入预算来消除该失败。定向报告`test-results/unity/ForestEdge-layout6.xml`，实际运行截图位于`test-results/forest-edge-screenshots/`与`test-results/city-map-screenshots/`，交付用5张原图及SHA256清单位于`test-results/unity/ForestEdge-delivery/`。

用户人工试玩、微信转换、真机、最终来源许可及正式性能验收未完成。本轮未提交或推送。
