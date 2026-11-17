# 资源资产与分包（按需）

触发任务：资源命名、导入、图集、Prefab/场景、加载与分包。

[返回规范入口](../../../CODE_CONVENTIONS.md) · 基础必读：[代码基础](../CODE_BASICS.md)、[项目基线](../PROJECT_BASELINE.md)。只读取任务相关小节；条款号沿用旧版，跨文件引用已转换为链接。

<a id="s-3-2"></a>

## 3.2 文件、资源与资产

| 类型 | 规则 | 示例 |
| --- | --- | --- |
| 脚本 | `PascalCase.cs` | `DailyRewardPopup.cs` |
| 面板 Prefab | `Panel_<模块>.prefab` | `Panel_Main.prefab` |
| 弹窗 Prefab | `Popup_<用途>.prefab` | `Popup_DailyReward.prefab` |
| 控件 Prefab | `Widget_<用途>.prefab` | `Widget_RewardCard.prefab` |
| 场景 | `SC_PascalCase.unity` | `SC_Main.unity` |
| 贴图 | `T_<模块>_<用途>_<变体>` | `T_UI_BtnClaim_Normal` |
| 图集 | `Atlas_<模块>_<用途>` | `Atlas_Popup_Daily` |
| 九宫格 | `Frame_<用途>` | `Frame_PopupBg` |
| 材质 / Shader | `M_*` / `Sh_*` | `Sh_UI_RoundedMask` |
| 音频 | `SFX_*` / `BGM_*` | `SFX_Popup_Open` |
| 动画 / 控制器 | `Anim_*` / `AC_*` | `AC_Popup_Daily` |

- **禁止**资源名含空格、中文、版本号后缀 `_new` / `_final` / `_bak`；历史版本交给版本库。
- **禁止**同名资源放在不同目录；重命名**必须**在 Unity 内操作以保 `.meta` 与 GUID 一致。

<a id="s-8"></a>

## 8. 资源与资产规范

<a id="s-8-1"></a>

## 8.1 导入设置（必须逐项确认，不接受默认值）

| 资源 | 关键项 | 移动端基线 |
| --- | --- | --- |
| 贴图 | 压缩格式 | Android ASTC / ETC2 兜底，iOS ASTC |
| 贴图 | `Read/Write Enabled` | 关闭（需运行时读像素时单独申请） |
| 贴图 | `Mipmap` | UI/2D 关闭；3D 场景按需 |
| 贴图 | `Max Size` | 按实际显示尺寸设定，禁止 2048 用在 64px 图标 |
| Sprite | 九宫格 Border | 底板/边框**必须**用九宫格，禁止为每个弹窗尺寸切多张整图 |
| Sprite | 图集 | 同面板进同一图集，禁止散图导致合批失败 |
| 音频 | `Load Type` / 压缩 | 短音效 Decompress On Load，BGM Streaming + Vorbis/ADPCM |
| 动画 | 精度 / 曲线 | 去除无用曲线，UI 动效优先用代码补间而非大序列帧 |

<a id="s-8-2"></a>

## 8.2 竖屏 UI 资源

- **必须**背景用纯色 / 九宫格 / 可平铺元素延伸到安全区外；**禁止**按 1080×1920 切死整屏背景图。
- **必须**按面板/弹窗分图集，跨模块共用需评审；**禁止**一张全局大图集常驻。
- **禁止**多层全屏半透明 Sprite 叠底造成 Overdraw；**必须**纳入真机验收。
- **推荐**长文本按钮、多语言标签使用自适应容器，避免为每种语言单独切图。

<a id="s-8-3"></a>

## 8.3 Prefab 与场景

- **必须**Prefab 编辑在 Prefab Mode 进行；修改前在版本库独占加锁，禁止并行改同一文件。
- **必须**Prefab 嵌套层级扁平稳定；**禁止**运行时通过名字路径访问嵌套节点。
- **禁止**提交调试用 GameObject、空脚本、断链组件、临时材质；测试场景统一放 `Assets/EditorScenes/` 且不参与打包。
- **推荐**一个面板/弹窗一个明确 Owner，减少多人同时改同一 Prefab 的冲突面。

<a id="s-8-4"></a>

## 8.4 资源分包与地址

- **必须**通过统一资源管理系统（Addressables / AssetBundle）按路径或标签寻址，**禁止**新增 `Resources/` 目录下资源。
- **必须**资源路径常量化或由生成代码提供，禁止硬编码字符串路径散落各处。
- **必须**同一资源只属于一个主包，共享依赖抽公共包，禁止重复打包。
- **必须**区分统一资源加载与远程内容更新：使用 Addressables / YooAsset / AssetBundle 不代表已具备代码热更新；可先采用本地资源模式，远程发布与代码热更新分别按 [§9.5](updates.md#s-9-5) 评审，禁止仅因引入资源框架就额外叠加另一套下载/缓存系统。
- **必须**首包 / 延后下载 / 按需下载清单与体积上限由立项评审确定，CI 逐版本校验增量；休闲项目**推荐**把非核心玩法资源延后下载。
- **必须**多渠道包（Android 渠道 / iOS / 小游戏等）共用同一套资源与配置；渠道包名、签名、密钥由 CI 注入，**禁止**入库。
