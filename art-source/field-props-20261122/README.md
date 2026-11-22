# 清障与逆塔防物件素材

2026-11-22。用户授权继续补清障及逆塔防素材，沿用此前指定本机ComfyUI与现有清新手绘画风。当前先补小院8类物件、12类猫狗玩具及合体、6机关、1爪印；spare-leaves只是末页余位辅助图，不扩玩法。参数/参考/提示见[jobs.json](jobs.json)。母图和历史留Unity外，派生运行页按实际尺寸裁切、去底并保存依据。纯白底输出不宣称原生透明。

初始规划：各母图768×768，运行场地图页约512档、逆塔防约512档；同阶段共享配置与贴图，独立文案及判定区，原规则不变。最大UI手牌图标约60实际像素、场内约20—50像素，候选切片上限128。唯一资源清单先登记planned/preview-only，现已导入并按实际静态引用计first-play；不自动approved或提高预算。实际验证与当前状态见[任务](../../docs/协作任务/20261122-关卡物件素材-01a1f74d.md)。

## 生成与派生

7个真实ComfyUI作业均完成，workflows/history/submission在各分项目录：garden `95e256d8-6a42-4b04-b2c7-9287b5689598`、cleanup `88d61040-9a8a-4401-a4be-498633039347`、cat-toys `61cec7c9-962c-4f6e-b15a-2c09b5cfac4e`、cat-upgrades `3c45dd30-a431-4b12-a52f-27eb0f15620c`、dog-toys `eb43abb9-307b-4fc4-a21e-002ac35b9dd1`、dog-upgrades `60fd1942-0b20-4db7-aff3-8a2ef3e3d5c9`、mechanisms `e12fcb26-1016-4604-b853-ec21871cb36a`。

[实际服务环境](system_stats.json)。既有Qwen Image2.1 INT8 ConvRot / Qwen3VL8B / Qwen VAE；形状参考为第一输入、小院为风格第二输入，resolution0、shift3.1、24步、CFG4、euler/simple、denoise1，seed2026112301—2026112307。所有提示/参数随实际workflow留存，没有以手绘图形冒充模型输出。

[derive.mjs](derive.mjs)按四格裁切、边界连通近白去底（RGB各通道≥244），对外缘2px做白底去污染，保留离散的绳须/气泡/泥点与内部浅色；裁边缩至场地124px、道具108px内，加透明边距，复用共享MaxRects组页。256色PNG量化后将纯色几何用的白角PLTE颜色校正为纯白并重算CRC。各源/派生哈希、尺寸与切片见[记录](derivation.json)。这会轻微调整同一最亮色阶，未声称无损真彩；母图保留。

实际两页为264×436/8切片/51709bytes与512×468/19切片/75046bytes，合计123.8KiB；场地树篱/石块/水沟/桥/箱/杂物/草/泥，逆塔防含12种基础/合体、6机关与1爪印。末格辅助叶片只作为reference留档，不计运行页。图页与控件文字独立，前景点击/清理范围保持原逻辑。

本轮服务启动时尚未监听，首次请求被拒绝，确认日志已就绪后才开始提交；没有失败生图被当成功。原图页真彩量较大，256色后源文件缩小；校验发现量化使白角偏奶白，已修正输出并重验为255/255/255/255。[深浅边缘](edge-review.jpg)与[图页检查](../../test-results/field-props/atlas-review.json)。本轮拥有的ComfyUI进程101736在确认队列为空、进程身份一致后停止，[记录](../../test-results/field-props/service-stop.json)，未停止他人服务。

## 接入

[Unity导入描述](unity-import.json)、[增量Prefab Mode来源](../../test-results/field-props/prefab-import.json)、[实现与当前验证](../../docs/关卡物件素材接入.md)。母图/派生参考均在Unity外，唯一运行条目为field-props.garden-atlas、field-props.siege-atlas，temporary/first-play；不重复算母图。视觉初筛待用户最终反馈，许可/微信/真机仍pending。

最终统一验证EditMode793/793、PlayMode84/84，1221项输入一致；[最终图鉴](../../test-results/field-props/final/sprite-preview.jpg)、[实际运行图](../../test-results/field-props/final/runtime-preview.jpg)、[截图与哈希](../../test-results/field-props/final/provenance.json)。Unity只读审计与台账均确认Main24图1868303bytes；两新页126755bytes，未提高源规划预算。具体最终报告/失败保留/试玩边界按实现记录与任务读取。
