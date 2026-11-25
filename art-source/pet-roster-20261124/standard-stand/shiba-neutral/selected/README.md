# 柴犬当前站姿母图

2026-11-25 用户选定四图对照最右一张：Qwen Image 2.1 原图参考 + VAE。不重新生成或调色，[站姿母图](standing.png) 是作业 `3154bba8-7a34-424c-9cef-26b3869a5689` 的 736×800 PNG，原样复制。SHA256：`f9be08d9e36266d0ab2a2bfc1d0e447a77dfd586936a2e829558b938e2046de2`。

柴犬拆层与绑骨以此图为基准，不沿用迈步图的骨点。[复现工作流](../qwen-fresh-study/reference-vae-workflow.json)和[对照说明](../qwen-fresh-study/README.md)在前轮目录。母图原样保存在 Unity 之外，救助站使用下文单独烘焙的动作页。

## 站坐预览

- [姿态对照](../../../sit-texture/shiba-selected-poses.jpg)按同一时间轴展示站立、下沉、后腿收折、坐定，浅底与深底各一排；GIF 用同一套动作倒放预览起身。
- [柴犬配置](../../../sit-texture/shiba-selected.json)独立标定关节与蒙版。前爪固定，后腿定长折叠；臀底只贴地收形，尾根随骨盆。两个近腿与胸腹的内侧交界仅在躯干覆盖区域羽化，不削掉外轮廓。
- 臀底贴地优化：最后 24% 的落座阶段，把近后腿与臀底的接地轮廓一起顺平，外侧仍圆滑回弯。没有改母图、骨点或两爪落点。局部前后对照位于预览输出目录的 `contact-comparison.jpg`。
- 按红线修正：追踪站姿尾根下的后臀外缘，落座时让这一段沿局部网格转向地面，尾根补跟随、卷尾不接地。预览输出目录的 `rump-roll-comparison.jpg` 对比原站姿标记、前版坐姿和同一段线在新版坐姿的位置。
- 进一步按红线顶点定位：站姿标注顶点 `[649, 422]` 随后臀转到坐姿 `[575, 730]`（可见接地带），卷尾随落座下移，末帧尾尖离设定地线 13 源像素，不承重。预览输出目录的 `tail-root-comparison.jpg` 只标这一个点，红点不进动画。
- 当前版固定卷尾下端相对同一红点的落位（右 5、上 1 源像素），重新旋转原尾图，不拉长尾巴；实测尾尖距设定地线 3 像素。坐下时局部顺平后颈到臀部的背部剪影，保留脚位和原图纹理。`tail-tip-back-comparison.jpg` 有前后和中途对照。
- `node tools/pet-roster-sit-texture.mjs shiba-selected`。生成透明关键帧和 `shiba-selected-sit.gif` 于 `test-results/pet-roster-20261124/sit-texture/shiba-selected/`；坐稳像素接地与骨长验证见同级 `checks-shiba-selected.json`。不需要上一轮的 ComfyUI 输出或迈步图清图目录。

## 救助站动作页

- `node tools/pet-roster-sit-texture.mjs shiba-station` 从同一母图、蒙版和骨点生成动作页和 `test-results/pet-roster-20261124/sit-texture/shiba-station/walk.gif`。独立预览不影响运行图。坐姿直接取已确认曲线，起身倒放，不改尾尖和背线。
- 走路以近后、随后近前，再换远侧的顺序移脚；支撑段爪随身体推进而相对向后走，摆动段抬脚回前方。肩髋轻微起伏；胸腿和臀腿仍用网格过渡，腿骨定长。前半步从立姿渐入循环。显示速度按房间尺度匹配短步幅，不改存档或游戏时间。
- 动作页为 8 列 5 行、每帧 128×128 的透明 PNG。前 16 帧用于站坐和起身，接着 16 帧循环走，再接 8 帧走入。Unity 按住客表现时钟选择 UV，与左右翻转共用一页；运行时不计算骨骼。没有独立趴卧，暂用已确认坐姿。现场、护理大图和其他动物不替换。
