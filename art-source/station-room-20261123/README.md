# 救助站原图拆层（临时）

2026-11-23，用户授权去右下水印、使用本机ComfyUI拆大家具再拼回，并给中央坐垫接实际行走阻挡。原附件351×619，原件`reference.png`不改；裁掉白边后336×600，非高清重制。商业来源与模型使用条件仍待确认，去水印不等于取得许可。

## 实际制作

本机ComfyUI 0.37.1独立8193服务，原生节点，无新装插件或模型。Qwen Image 2.1：`qwen_image_2.1_int8_convrot.safetensors`、`qwen3vl_8b_int8_convrot.safetensors`、`qwen_image_2.1_vae_bf16.safetensors`。各作业`workflow.json`、`submission.json`、`history.json`和RGBA `master.png`保留真实参数/提示/作业ID。24步、Euler/simple、CFG4、shift3.1、denoise1、resolution0；不是仅凭seed承诺跨环境像素复现。

| 作业 | seed | 实际用途与限制 |
| --- | --- | --- |
| clean | 2026112301 | 去水印全图输出；仅取右下局部补丁，避免全图漂移 |
| background | 2026112302 | 清空家具；漏掉右侧柜组，未直接采用 |
| bed | 2026112303 | 透明中央坐垫 |
| tree | 2026112304 | 透明爬架；盆栽被拆成悬空叶片，后处理清掉 |
| rear | 2026112305 | 靠枕与右侧柜/其盆栽，分成两切片 |
| plant | 2026112306 | 错提右侧柜上植物且位移，失败候选保留，不入运行图 |
| empty | 2026112307 | 在background上去掉遗漏柜组，最终空房底图 |
| plant-local | 2026112308 | 原图局部54×78裁切放大到224×320去背景，再缩回原位置；非新增高清细节 |

全图模型工作尺寸352×608；`derive.mjs`恢复336×600。`cleaned.png`原图仅[260,565,76,35]内羽化修补水印，其余像素逐字节不改。空房与独立家具含生成造成的轻微线条/颜色差异，不宣称无损抠图。透明处理阈值、区域限制、裁切和坐标均在脚本及`derivation.json`。盆栽没有遗漏花盆，错误作业不偷偷覆盖。

## 运行图

`T_StationRoom_Background.jpg`为336×600 JPEG，35,490 bytes；五个家具切片共用`T_StationRoom_Furniture.png`，496×248，142,962 bytes。共174.3KiB源图，在本批200KiB+150KiB计划内。`assembled.png`是重组预览，`edge-review.png`为深浅底透明边核对。PNG母图/中间件不进Unity。8193曾经用户授权与动物过渡会话共享；对方明确归档/释放后，16:51核对本任务PID与空队列并停止，逐项校验SHA256后仅清理本任务12份重复服务图片，对方子目录保留不动。

`node art-source/station-room-20261123/derive.mjs`仅派生已保存作业；`run.mjs run <slug>`只在队列空闲且无已提交记录时排入新作业，已提交只用`collect`，禁止重复提交。原图留存，母图与输入路径由工作流可追溯。

Unity通过独立隔离副本运行`tools/station-room.ps1`，只增量编辑保存的站内Prefab，不重建其他面板，不控制用户Editor。背景/家具共用等比坐标空间，挂画、窗帘等墙面小装饰留背景；动物/笼子继续来自救助状态。中央坐垫、爬架/盆栽等足迹通过`StationRoomObstacle`显式接到实际游走的线段检查，不用断开的Collider2D冒充阻挡；不包含完整寻路、攀爬、家具交互或动物间碰撞。

首屏静态依赖按`temporary/first-play`登记，不把面板隐藏称为按需下载。微信转换、AOT、真机和正式来源许可仍未验。
