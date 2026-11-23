# 原图短腿萌系坐下候选

2026-11-23。用户认可结构动作，但不接受长腿导致原图风格丢失。本批保留原图1:1头身纹理坐标，另建短骨架重定向，详见[模块说明](../../docs/猫坐下萌系比例.md)。

- 原图`public/pet-mesh/cat-side.png`，452×512。头脸/围巾/身体直接按原坐标裁出，不重生、不改源文件、不拉长；原图完整PNG继续用于对照。
- 腿/尾/隐面取已留档pet-sit-skin图集和hind-source.png中的同源部件，保持原尺寸。后腿下段被毛刚性随跗关节转动，局部alpha渐隐遮接件，供体近侧/原色不另换动物。原图与两供体文件SHA在plan.json锁定，变化会拒绝派生，须重审来源/绑定。
- 8部件、344×712、169040bytes。派生脚本保留mask、渐隐范围、原坐标、alpha接地样本与图集布局，2px透明间隔。未使用ComfyUI，没有新模型/种子/云调用；之前供体的生成依据仍在原批次，不伪造本轮生成记录。
- 仅candidate/preview-only，计划393216bytes不提高。无Unity资源引用/导入、无提交推送，旧骨架14项、长腿贴图26项、旧失败贴图55项输入冻结不覆盖。

```powershell
node tools/pet-sit-chibi-process.mjs
node tools/pet-sit-chibi-process.mjs --check
node tools/pet-sit-chibi-audit.mjs --freeze
node --test tests/pet-sit-chibi.test.mjs
node tests/pet-sit-chibi-browser.mjs --animation
node tools/pet-sit-chibi-audit.mjs --verify
npm run assets:check
```

`--check`重新派生后逐字节核对，不写输出；来源变化先失败，不静默套用旧审核。普通命令只更新本批运行图和derivation，不碰原图或前批。独立冻结输入在`test-results/pet-sit-chibi/verification-inputs.json`，实际截图、GIF、原图/绑定站姿对照与浏览器统计同目录；不把源图比例指标当正式美术验收。

最终新8项与旧13项单测共21/21通过，浏览器六视口54时点通过；27项本批输入和旧14/26/55项输入全部核对未变。普通/叠骨GIF各61帧7300ms。唯一资源台账检查通过，本批无findings；肩/臀腿接缝和远爪差异仍待观感精修，不把自动检查当正式美术通过。

后续臀尾触地版已获用户接受；提交记录见[制作经验](../../docs/动物同骨架过渡经验.md)。本次候选树重新验证四阶段共33/33，最终短腿版六视口54时点通过。`verification.json`保存最终27项动作输入、提交候选验证边界与统计；[五姿态](accepted/contact.png)和[原图/绑定站姿](accepted/source-standing.png)为实际候选浏览器输出，不是示意重画。GIF仍保留本机证据目录，可由浏览器脚本重新导出，不额外把多MB动画当运行素材。台账只将visual置accepted，candidate/preview-only与rights/target pending保留。
