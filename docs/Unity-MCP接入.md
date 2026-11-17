# Unity MCP 接入

日期：2026-11-17。用户授权为当前工程选择并接入 MCP 或 bridge。

## 选型与边界

- 采用 [CoplayDev MCP for Unity](https://github.com/CoplayDev/unity-mcp) 10.0.0，MIT 开源；支持当前 Unity 2022.3.62f3。
- UPM 固定 Git commit `d49ae2953580f3481beb1e084a1da2682f0b5610`（v10.0.0），服务固定 PyPI 包 `mcpforunityserver==10.0.0`，不跟随 main 自动升级。
- 沿用本机 Codex 已有的 `unityMCP` 配置：`http://127.0.0.1:8080/mcp`。没有配置其他 AI 客户端或远程服务。
- 只监听 IPv4 回环地址，不开放 LAN、不配置公网、账号或 API Key。本地 HTTP 没有鉴权，不应转发此端口。
- 不自研第二套 bridge。现有批处理构建/测试脚本继续保留；MCP 用于编辑器交互，不替代发布验证。
- 插件依赖新增 animation、physics、physics2d、screencapture、unitywebrequest 内置模块及 Newtonsoft JSON；Test Framework 保持工程原有 1.1.33。
- 操作工具位于第三方 Editor 程序集。上游另含跨平台 `MCPForUnity.Runtime` 辅助程序集，不能称为完全零运行时依赖；Player 构建剔除与新增依赖体积尚未验证。
- 没有新增项目业务脚本、场景物体或玩法规则。后续操作必须先核对工程实例，不能误操作本机其他 Unity 工程。

## 启动与连接

在仓库根目录运行，要求 PowerShell 7、uv/uvx、Python 3.10+：

```powershell
pwsh -NoProfile -File tools/unity-mcp.ps1 -Task Start
pwsh -NoProfile -File tools/unity-mcp.ps1 -Task Status
```

Start 在后台无窗口启动固定版本服务，已有兼容服务时复用，不终止占用端口的其他进程。首次需联网下载 Python 依赖。由脚本新启动的进程禁用 MCP 遥测；复用服务不会改变其环境。脚本日志位于忽略的 `test-results/unity-mcp/`。

当前连接由 Unity 自启服务承载，`manage_editor(telemetry_status)` 实测为 true；不能宣称当前遥测已关闭。Unity 侧偏好也尚未验证。希望禁用时，先在 MCP 窗口停止本机服务，再使用上述 Start 命令重启并 Connect；不要同时让 Unity 自动启动另一份服务。

Unity 打开 `unity/HomelessPetHelper`，等待编译，进入 `Window > MCP for Unity`：

1. 使用 HTTP Local，URL 为 `http://127.0.0.1:8080`（Unity 侧为基础 URL，不加 `/mcp`）。
2. 连接区点击 Connect，建立 Unity 会话；不要把“服务 healthy”误认为“编辑器已连接”。
3. Status 必须输出 `editorConnected: true` 和唯一的 HomelessPetHelper 实例。多个实例时使用完整的 `HomelessPetHelper@hash` 定向操作。

当前聊天若仍没有 Unity MCP 工具，可使用同一服务的官方 CLI，不必重装插件：

```powershell
uvx --from mcpforunityserver==10.0.0 unity-mcp --format json instances
uvx --from mcpforunityserver==10.0.0 unity-mcp --instance 'HomelessPetHelper@<实际hash>' scene hierarchy
uvx --from mcpforunityserver==10.0.0 unity-mcp --instance 'HomelessPetHelper@<实际hash>' editor console --type error
```

占位 hash 必须来自 instances 的实际结果。当前工程实测为 `HomelessPetHelper@d85036a837261228`，重建/移动工程后应重新发现。聊天原生 MCP 工具是否已加载，以该聊天的可用工具为准；CLI 可用不等于工具列表已刷新。本次原生工具未出现在聊天列表，但已通过官方 Python MCP 客户端和 CLI 调通同一 HTTP 服务。

## 本轮验证

- UPM manifest 与自动生成 lock 已登记固定版本；编辑器完成导入，已生成 MCPForUnity.Editor / Runtime 程序集，Editor.log 未报告 C# 编译错误或警告。
- 服务 `/health` 返回 healthy、版本 10.0.0；监听地址实测为 `127.0.0.1:8080`。
- 2026-11-17 解锁后首次连接成功；Status 输出 `editorConnected: true`，Unity 版本与工程绝对路径均核对正确。
- MCP 协议 initialize、工具发现、set_active_instance、editor/state 与 project/info 资源读取成功；不是仅用 health 推断连接可用。
- 场景层级与 Console 读取成功；当前打开未保存的 Untitled 场景，仅有 Main Camera，不是正式 Main 场景的玩法验收。
- Play、暂停、恢复、Stop 均成功；已实际观察到 playing true/false，最后恢复停止。场景仍为 `isDirty: false`，未保存、覆盖或加载其他场景。
- 相机截图成功，文件在忽略的 `unity/HomelessPetHelper/Library/MCPForUnity/Smoke/`，未生成 Assets 临时资源。
- 最终 Console 查询返回 0 条 error/warning；进入 Play 的 Domain Reload 中曾观察到上游 WebSocket 重连警告，随后恢复连接。
- 初次 `node tools/check-unity.mjs` 通过，PowerShell 脚本语法解析通过；连接完成后再次检查时，其他并行工作刚新增 UI 目录/脚本，报告 5 项缺少 meta，本轮没有替其生成或覆盖，不能称最终全仓源码检查通过。详见 `test-results/unity/source-check.json`。
- 本轮不重复执行其他聊天正在进行的业务规则测试，也未验证 Player、微信或真机；安装插件不构成这些检查通过。

## 当前限制

- 上游 10.0.0 的 editor/state 在 Play 中把 `isPlayingOrWillChangePlaymode` 当作切换状态，可能长期返回 `is_changing: true`；稳定状态缓存也可能被标记 stale。不能无期限等待这个字段变 false；结合 `is_playing`、编译状态、实际工具结果与 Console 判断。Domain Reload 返回 retry 时应有界重试，不把瞬时断连当作业务失败。
- `execute_code` 使用本机 CodeDom 后端实测编译失败，错误为 `Line 1: BOM`；未执行所请求 C#，没有修改第三方缓存或关闭安全检查。当前不依赖任意 C# 即时执行，代码仍按正常文件编辑与 Unity 编译流程处理。后续按需单独修复/升级验证。
- 以上是本机版本的接入边界，不代表场景、资源、测试、构建等全部工具都已逐一验收。

## 撤回

从 Package Manager 移除 `com.coplaydev.unity-mcp` 并让 Unity 重新生成 lock；不手改生成缓存。关闭连接，停止确认属于本次服务的进程。若希望保留给其他工程使用，不删除本机原有 `unityMCP` 配置。不要按端口批量终止未知进程。

人工复核由用户或指定 Reviewer 完成；后续升级需再次验证 Unity 编译、只读操作及受影响构建。
