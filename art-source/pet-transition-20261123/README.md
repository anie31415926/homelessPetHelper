# 同源动物站坐制作链 v0.2

2026-11-23。本批是橘猫独立浏览器样例，不是实际 Spine 工程或 Spine SDK 接入。调研/范围/验收见[模块记录](../../docs/动物同骨架过渡制作与调研.md)。

本页记录早期生成/失败对照链，当前用户接受版本为[短腿萌系与臀尾触地](../../docs/猫坐下萌系比例.md)，可复用经验见[总结](../../docs/动物同骨架过渡经验.md)。提交保留源母图、工作流和历史，排除运行时server副本/PID/日志；台账提交版本只引用本批规范归档，不依赖另一会话服务目录。

## 已冻结的输入

- 身份基准：`public/pet-mesh/cat-side.png`，452×512；SHA256见plan.json。头脸、耳朵和围巾直接复用原像素，不消耗生成脸。
- 补画：`torso-a/master.png`；选择与文件SHA256在selection.json。只在原坐标y330..444、躯干mask内部混入生成颜色；原图上部花纹保留。生成图仍保留了裁区顶部的围巾，不把它当作完美自动拆图，也不消费该生成围巾。
- 输入裁区74,270,330,242，guide768×568，Qwen实际输出896×672。回原坐标只作用于局部供体纹理，不按姿势外接框缩放整只动物。后处理保留原alpha/明确mask，半透明拼接根部的处理见process脚本；不是latent masked inpaint。
- 模型/编码器/VAE三文件的实际大小、SHA256，以及ComfyUI0.37.1 / PyTorch2.10.0+cu130 / Python3.13.12 / RTX4080记录在reproducibility.json。
- 固定seed2026112301，24步、CFG4、Euler/simple、AuraFlow shift3.1；完整正负提示/API节点保存在workflow.json。不要只复制seed。

## 重建，不需要重新生图

从仓库根执行，使用现有Node依赖：

```powershell
node tools/pet-transition-process.mjs
node tools/pet-transition-process.mjs --check
node --test tests/pet-transition.test.mjs
node tests/pet-transition-browser.mjs --animation
npm run assets:check
node tools/pet-transition-audit.mjs --verify-snapshot
```

`--check`逐字节比较派生层/图集/清单，不写输出。原身份图或选中母图SHA改变会拒绝继续；没有选图或母图就失败，不静默退回别的图。图集复用公共image-atlas工具，坐标不随裁边改变。

本轮验证输入由verification-inputs.json保存；后续修改先检查差异，再重验，不要只重录指纹代替测试。

预览沿用本机既有Vite5193：`http://127.0.0.1:5193/pet-transition.html`。服务未启动时用`npm run dev -- --port 5193 --strictPort`；端口被其他服务占用则选择空闲端口，并同步浏览器测试URL。本页未加入正式多入口构建列表，独立编译已验证，不自动混入原型默认发布或Unity。

## 新生成，另建作业目录

这是可执行的 **ComfyUI API工作流**，不是已验证的拖入前端UI布局JSON。脚本会上传本地参考、检查节点存在、提交一次并保存prompt_id；fetch按同一id续取，不重新计次/生成。

先核对本机GPU与其他ComfyUI任务。需要独立服务时，向`start-comfy.ps1 -Port 8194`传入`-Backend`（含main.py与.venv的既有ComfyUI目录）和可选`-ModelPathsConfig`，也可通过COMFY_HOME/COMFY_MODEL_PATHS环境变量指定；不硬编码个人安装路径。它仅绑定回环、使用本批server目录、记录自己PID，不修改Desktop或下载模型。启动脚本仅做语法/缺参拒绝检查，修改后的脚本未实际启动服务；本轮实际生成复用了经协调的8193服务。不要在显存占满时盲目再开。

只有确需重新审核已安装权重时才运行无参数audit命令，须显式设置COMFY_MODEL_ROOT；`--snapshot/--verify-snapshot`不访问模型或ComfyUI。已有reproducibility.json是当时实测原件，本次提交未联网重生或重写该记录。

```powershell
$env:COMFY_URL = 'http://127.0.0.1:8194'
$env:PET_SEED = '2026112301'
node tools/pet-transition-art.mjs prepare torso-next
node tools/pet-transition-art.mjs submit torso-next
node tools/pet-transition-art.mjs fetch torso-next
```

prepare拒绝覆盖已准备目录，submit拒绝已有submission.json。超时或取图中断只运行同一作业的fetch；提交结果不明时先查队列/历史，不盲重提。新结果不自动替换selection.json，不自动approved；必须看深浅底和实际过渡。新增图同时登记唯一资源台账，server输入/输出副本也按reference登记。

## 稳定性的实测边界

torso-a与torso-repeat是两个不同prompt_id的实际作业，耗时45.776s / 27.017s。相同模型/参考/提示/seed，解码RGBA的SHA256相同，平均/最大像素差都是0；PNG文件因历史元数据不同，文件SHA不同。母图、参考、工作流、提交及历史均已保存。

这证明本机该补画可重复，不证明跨seed、服务重启、不同GPU/模型版本、侧卧或不同品种自动稳定。当前骨点、mask与权重为本猫定制。广泛生产前仍需另一只动物和另一姿势复核，不把本批做成无人审核批量入口。

## 已有与未有

已有：11附件/21骨共享一个Skeleton，原图头部不缩放，站坐始终使用同一图集，无两身体透明淡入。后腿定长髋/膝/跗/脚链，髋下降并驱动大腿旋转、官方CCDIKSolver折叠膝/跗；脚掌独立绑定固定目标，大腿刚性绑定，禁止靠整片宽权重压缩。姿势可暂停/任意拖动/反向采样；11项单测包括101姿态骨长/体积/足端与确定性回归。

后腿每侧拆成Thigh/Shin/Paw。Shin命名在此代表可见跗部附件，膝到跗的段主要藏于大腿下；不是完整写实动物解剖图。Thigh末端只在mask内部、原坐标y>410缺像素处用同列上方邻近不透明毛色闭合；不重画脸、不放大纹理。派生mask/远侧偏移/脚掌渐入区域在derivation.json留档。旧nearHind/farHind整片PNG只留历史引用，不再进入图集。当前图集340×740、217327bytes。

旧压缩版源码/图集/输入快照/接触表/GIF留在baseline-compressed，不覆盖以便对照。最新正常/后腿骨架的实际动画在test-results/pet-transition/stand-sit-bones.gif；骨架开关只画后腿三节，不画根控制器到髋的辅助连线。

待看：肩/髋渐变接缝、关节遮挡、尾根衔接和落坐的重心观感。定长保形不等于线条/解剖已达正式美术，本轮只是Q版运动学近似。用户确认前不替换Unity现有动物；侧卧、骨骼走路间衔接、清洁遮罩与目标平台均未接入或未验。没有重新使用ComfyUI、生成/安装商业Spine项目或替代其许可。
