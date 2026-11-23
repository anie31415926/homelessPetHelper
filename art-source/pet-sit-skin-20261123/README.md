# 已认可骨架的同源猫贴图候选

2026-11-23。用户认可无贴图坐下骨架后授权接到图中，本批仅独立Web预览，不接Unity。具体绑定、检查与未验边界见[模块记录](../../docs/猫坐下同骨架贴图.md)。

## 来源与派生

没有新ComfyUI作业，未使用或占用其他会话GPU服务。只读复用`public/pet-transition/atlas.png/json`、`art-source/pet-limbs/donor/front-source.png`和`hind-source.png`，四项文件SHA256在派生清单中。旧图集/原图/骨架输入均不覆盖。

8个图集部件：head/torso/tail/nearHindPaw为旧图集裁区，nearFront为无渐隐的原腿供体，nearHindThigh为闭合遮罩原腿供体，shaft/neck为同源纹理行重采样的闭合重叠面。大腿遮罩缺少隐藏像素处只取同列上方不透明毛色，不把它当作真实隐藏解剖；不是重新设计动物或模型生图。head只移除旧附件夹带的围巾下躯干，脸/眼/耳主体原RGBA逐字节保留。

图集344×732，175294bytes，四周2像素透明间隔；线性采样且不用mipmap避免邻件串色。计划393216bytes保持原值，仅preview-only/candidate，许可/正式视觉/目标端仍pending，不能据骨架已认可把新贴图改成approved。

```powershell
node tools/pet-sit-skin-process.mjs
node tools/pet-sit-skin-process.mjs --check
node tools/pet-sit-skin-audit.mjs --freeze
node --test tests/pet-sit-skin.test.mjs
node tests/pet-sit-skin-browser.mjs --animation
node tools/pet-sit-skin-audit.mjs --verify
npm run assets:check
```

process的`--check`重新派生后逐字节核对图集/两份清单，不写文件。普通命令只重建本批目录，源路径和处理参数在脚本中明确；四项源哈希与plan不符即拒绝重建，须重新检查来源与绑定，不静默套用旧审核。验证输入包含只读骨架/样式、源图/源清单、工具、绑定、测试和Three.js依赖；14项骨架旧快照与55项贴图失败对照快照另外核对，不覆盖历史快照。

新图计划先记录在本批plan.json；共享图片台账因Loading同时认领暂缓。用户授权发协调消息后，对方已于19:27在自身任务确认各自命名空间/写前复读/局部合并；之后只新增`pet-sit-skin.atlas`。写前后排除此命名空间的完整结构SHA256相同，保留Loading视觉接受及其他条目，未改预算。后续修改仍需重新检查占用，不把本次协调当永久授权。
