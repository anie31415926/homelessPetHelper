# 家里电脑 · Unity MCP 连接配置说明

日期：2026-11-26。适用于**家里这台 Windows 本机**；换机、重装或工程搬家时按[换机要改的参数](#换机重装要改的参数)逐项替换。

## 一句话结论

本机走 **stdio 通道**：Unity 编辑器里的 MCP for Unity 插件起一个 TCP 命令桥 `127.0.0.1:25000`，WorkBuddy 侧由 `uvx` 拉起 `mcpforunityserver==10.0.0` 以 `--transport stdio` 连上该桥并翻译 MCP 协议。

**不要**按 `docs/Unity-MCP接入.md` 里写的 HTTP 8080 模式来配本机 —— 那需要 Unity 窗口切 HTTP Local 再点 Connect，本机编辑器跑的是 stdio 桥，实测 8080 服务 healthy 但工具调用返回 `no_unity_session`。

## 版本与路径清单（本机实测）

| 层 | 值 |
| --- | --- |
| Unity | `2022.3.62f3`，`D:\Program Files\Unity 2022.3.62f3\Editor\Unity.exe` |
| 工程 | `D:\PetHelper\homelessPetHelper\unity\HomelessPetHelper` |
| UPM 包 | CoplayDev MCP for Unity **10.0.0**，`Packages/manifest.json` 固定 git commit `d49ae2953580f3481beb1e084a1da2682f0b5610`（不跟随 main 升级） |
| Python 服务 | `mcpforunityserver==10.0.0`（内部 FastMCP 3.4.11），PyPI 包固定版本 |
| uvx | `C:\Users\Administrator\.workbuddy\binaries\python\envs\default\Scripts\uvx.exe` |
| WorkBuddy 配置 | `C:\Users\Administrator\.workbuddy\mcp.json` 的 `UnityMCP` 条目 |
| 授权记录 | `C:\Users\Administrator\.workbuddy\mcp-approvals.json` |
| 工具列表缓存 | `C:\Users\Administrator\.workbuddy\mcp-tool-list.json` |
| 编辑器侧命令桥 | TCP `127.0.0.1:25000`，协议 `WELCOME UNITY-MCP 1 FRAMING=1` |
| 端口/beacon | `~/.unity-mcp/unity-mcp-port-e911422f.json`、`unity-mcp-port.json`、`unity-mcp-status-e911422f.json` |
| 系统动态端口范围 | **1024–15000**（`netsh int ipv4 show dynamicport tcp`：启动 1024 / 13977 个），非 Windows 默认 49152–65535 |

## 配置全文（可照抄）

`~/.workbuddy/mcp.json` 里 `mcpServers.UnityMCP`：

```json
"UnityMCP": {
  "command": "C:\\Users\\Administrator\\.workbuddy\\binaries\\python\\envs\\default\\Scripts\\uvx.exe",
  "args": [
    "--from", "mcpforunityserver==10.0.0",
    "mcp-for-unity",
    "--transport", "stdio",
    "--default-instance", "HomelessPetHelper"
  ],
  "env": {
    "UNITY_MCP_PROJECT": "D:\\PetHelper\\homelessPetHelper\\unity\\HomelessPetHelper",
    "CLAUDE_PROJECT_DIR": "D:\\PetHelper\\homelessPetHelper\\unity\\HomelessPetHelper",
    "NO_PROXY": "127.0.0.1,localhost",
    "no_proxy": "127.0.0.1,localhost",
    "PYTHONIOENCODING": "utf-8",
    "UNITY_MCP_DISABLE_TELEMETRY": "1",
    "MCP_DISABLE_TELEMETRY": "1"
  }
}
```

要点：
- **不能有 `disabled: true`**。旧配置是 `{"url":"http://127.0.0.1:8099/mcp","disabled":true}`，8099 根本没有服务监听，所以工具列表一直是空的。
- `NO_PROXY` / `no_proxy` 必须设：本机有本地代理，不设可能把回环请求劫持走。
- `--default-instance` 用项目名即可；同机开多个 Unity 实例时改成 `HomelessPetHelper@<hash>`，hash 从 `instances` 查询取。
- `UNITY_MCP_PROJECT` / `CLAUDE_PROJECT_DIR` 指向 Unity 工程根（不是仓库根）。实测 cwd 为仓库根时靠这两个变量也能连上。

## 链路与协议

```
WorkBuddy ── stdio / MCP JSON-RPC ──> uvx mcp-for-unity ── TCP 25000（长度帧）──> Unity Editor 插件
                                      （协议翻译层，不能省）                    StdioBridgeHost
```

- **25000 不是标准 MCP 端点**。它是编辑器侧的内部命令桥，直接发 MCP JSON-RPC 会回 `{"status":"error","error":"Command type cannot be empty"}`。
- 握手后每帧格式：**8 字节大端 UInt64 长度 + UTF-8 payload**（`StdioBridgeHost.WriteFrameAsync` / `ReadFrameAsUtf8Async`）。
- 桥内部认 `command` 字段；`ping` 帧直接回 `pong`。所以必须经 Python server 翻译，别跳过。
- 插件还会写 `~/.unity-mcp/unity-mcp-status-<hash>.json`，`reason: ready` + 心跳时间表示桥活着。

## 首次启用步骤

1. Unity 打开 `unity/HomelessPetHelper`，等编译完成、Console 无 error。
2. 确认桥已起：`netstat -ano | findstr ":25000"` 应看到 `LISTENING`，PID 是**主编辑器**（不是 AssetImportWorker）；`~/.unity-mcp/unity-mcp-status-*.json` 为 `reason: ready`、端口 25000。
3. 确认 `mcp.json` 的 `UnityMCP` 条目如上，无 `disabled`；JSON 能被解析。
4. WorkBuddy：**连接器管理页右上角的自定义连接器入口 → 对 UnityMCP 点 Trust**。授权会写进 `mcp-approvals.json`，工具列表缓存进 `mcp-tool-list.json`（本机实测 46 个工具，含 `run_tests` / `get_test_job` / `read_console` / `refresh_unity` / `manage_editor` / `set_active_instance`）。
5. **重开一个会话**才能看到原生 Unity MCP 工具 —— 工具列表是会话启动时的快照，Trust 之后当前会话不会自动刷新。
6. 验证：调 `read_console`（`action=get`, `types=["error"]`）返回 `success: true` 即通；`manage_editor` 的 `telemetry_status` 本机实测 `telemetry_enabled: false`。

## 验证方法

### 只读确认（无副作用，优先用）

```bash
netstat -ano | grep ":25000"
cat ~/.unity-mcp/unity-mcp-status-*.json
python -c "import json,io;d=json.load(io.open('C:/Users/Administrator/.workbuddy/mcp.json',encoding='utf-8'));print(d['mcpServers']['UnityMCP'])"
```

### MCP 实调（当前会话没有原生工具时用）

用 Python 起 stdio server 并握手，最小片段：

```python
import subprocess, json, threading, queue, time, os
UVX = r"C:\Users\Administrator\.workbuddy\binaries\python\envs\default\Scripts\uvx.exe"
env = dict(os.environ); env.update({"NO_PROXY":"127.0.0.1,localhost","PYTHONIOENCODING":"utf-8"})
p = subprocess.Popen([UVX,"--from","mcpforunityserver==10.0.0","mcp-for-unity",
                      "--transport","stdio","--default-instance","HomelessPetHelper"],
    stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
    text=True, encoding="utf-8", errors="replace", bufsize=1, env=env,
    cwd=r"D:\PetHelper\homelessPetHelper")
q = queue.Queue()
threading.Thread(target=lambda: [q.put(l.rstrip()) for l in p.stdout], daemon=True).start()
def send(o): p.stdin.write(json.dumps(o) + "\n"); p.stdin.flush()
def wait(i, secs=30):
    end = time.time() + secs
    while time.time() < end:
        try: v = q.get(timeout=.4)
        except queue.Empty: continue
        try: o = json.loads(v)
        except Exception: continue
        if o.get("id") == i: return o
send({"jsonrpc":"2.0","id":1,"method":"initialize","params":{
    "protocolVersion":"2024-11-05","capabilities":{},
    "clientInfo":{"name":"probe","version":"1.0"}}}); wait(1)
send({"jsonrpc":"2.0","method":"notifications/initialized"})
send({"jsonrpc":"2.0","id":2,"method":"tools/call","params":{
    "name":"read_console","arguments":{"action":"get","types":["error"],"count":"10"}}})
print(wait(2)["result"]["content"][0]["text"][:800])
p.terminate()
```

- `tools/list` 应返回 46 个工具；`tools/call` 能拿到真实 Console 数据即为连通。
- 官方 CLI `uvx --from mcpforunityserver==10.0.0 unity-mcp --format json instances` 依赖 **8080 HTTP 服务**，本机 8080 没起时会超时，别用它判断连通性。
- HTTP 服务可用 `pwsh -NoProfile -File tools/unity-mcp.ps1 -Task Start` 起（端口 8080），但 Unity 不 Connect 就没有实例。

### 排障：直接探桥（有副作用）

```python
import socket, struct
s = socket.create_connection(("127.0.0.1", 25000), 3)
print(s.recv(64))                          # WELCOME UNITY-MCP 1 FRAMING=1
p = b"ping"; s.sendall(struct.pack(">Q", len(p)) + p)
h = s.recv(8); n = struct.unpack(">Q", h)[0]
print(s.recv(n))                           # {"status":"success",... "pong"}
```

**警告**：桥同一时刻只允许一个客户端，新连接会踢掉旧 client。做这个测试时原生 MCP 会断连，只在排障时用，测完别留着连接。

## 本机实测坑

1. **8099 错配**：旧 `UnityMCP` 指向 `http://127.0.0.1:8099/mcp` 且 `disabled:true`，该端口无监听 → 工具列表空。已改成上面的 stdio 条目。
2. **8080 Healthy ≠ 已连接**：`/health` 返回 healthy、10.0.0，但 `/api/instances` 为 `[]`，工具调用回 `no_unity_session`。HTTP 模式要 Unity 窗口用 HTTP Local（`http://127.0.0.1:8080`，不加 `/mcp`）点 Connect；本机编辑器是 stdio 桥，别混用。
3. **6400/6401 端口冲突 → 固定 25000**：本机动态端口范围是 1024–15000，6400/6401 落在临时端口池里，别的进程（实测 `WeChatAppEx.exe`）可能拿它当出站源端口，Unity 再 bind 得到 **WSAEACCES 10013**（不是 10048）；上游降级逻辑只捕获 `AddressAlreadyInUse`，所以不自愈、Console 反复报错。
   修复：把 `unity_port` 写成 **25000**，同时写 `~/.unity-mcp/unity-mcp-port-<hash>.json` 和 legacy 兜底 `~/.unity-mcp/unity-mcp-port.json`，且 `project_path` 必须等于 `Application.dataPath`（本机为 `D:/PetHelper/homelessPetHelper/unity/HomelessPetHelper/Assets`）。下次 domain reload 生效。
   **不要点 MCP 窗口的 Auto-Connect** —— 它的 `DiscoverNewPort()` 会优先复用 6400，等于改回冲突区。根治（需管理员）：`netsh int ipv4 set dynamicport tcp start=49152 num=16384`。排查时先 `Get-NetTCPConnection -LocalPort 6400` 看占用者，**不要按端口杀进程**。
4. **单客户端**：25000 桥只允许一个 client。原生 MCP 与临时 stdio 脚本、或两个 stdio server 同时跑会互相踢。别同时开两份。
5. **代理**：本机有本地代理，`NO_PROXY`/`no_proxy=127.0.0.1,localhost` 必须带上，否则回环连接可能被劫持。
6. **遥测**：Unity 自启的服务可能 telemetry 为 true；配置里加 `UNITY_MCP_DISABLE_TELEMETRY=1` / `MCP_DISABLE_TELEMETRY=1`。本机实测 `manage_editor(telemetry_status)` → `telemetry_enabled:false`。
7. **工具列表是会话快照**：Trust 之后当前会话仍看不到原生工具，必须重开会话。在此之前用上面的 stdio 客户端脚本即可，能力一样。
8. **`execute_code` 不可用**：上游 CodeDom 后端实测报 `Line 1: BOM`，不要依赖任意 C# 即时执行，代码仍走正常文件编辑 + Unity 编译。
9. **编辑器有编译错误时别跑 `run_tests`**：Console 残留的 `error CS…` 会让当前编辑器跑不了测试。批量验证仍用与 HEAD 一致的隔离副本（`tools/unity.ps1 -Isolated`），不要在有未提交中间态改动的编辑器里跑。
10. **`editor/state` 在 Play 中可能长期 `is_changing: true`**，不要无期限等它变 false；结合 `is_playing`、Console 和实际工具结果判断。

## 换机/重装要改的参数

- 所有 `C:\Users\Administrator\...` 与 `D:\...` 绝对路径（uvx、mcp.json、Unity、工程）。
- `UNITY_MCP_PROJECT` / `CLAUDE_PROJECT_DIR`：改成新机的 Unity 工程根。
- `--default-instance`：项目名变了就改；多实例时加 `@hash`。
- `~/.unity-mcp/unity-mcp-port-<hash>.json` 的文件名 hash 与 `project_path`：工程路径变了 hash 会变，需重新生成（旧的本机是 `e911422f`；另一个 `be49f727` 属于 `D:/MyWork/HanziGame`，别混用）。
- 端口 25000：若新机动态端口范围正常，6400 冲突可能不复现，但仍建议固定 25000 避开池内。
- UPM commit `d49ae295…` 与 `mcpforunityserver==10.0.0` **不要动**，两端版本必须配套。
- 本机 `mcp.json` 里另外 3 个 server（ninfer-local、adai-sketchup-managed、comfy-mcp）与 Unity 无关，保持 `disabled: true` 即可。

## 相关文件

- `docs/Unity-MCP接入.md` — 11-17 选型、首轮验证、`stdio 端口冲突修复（2026-11-24）`、当前限制与撤回步骤。
- `tools/unity-mcp.ps1` — HTTP 8080 服务的 `Start` / `Status`（本机默认不用）。
- `docs/工程验证入口.md` — 隔离副本验证方式。
- `~/.unity-mcp/stdio.js` — 上游新版 HTTP-endpoint 转发器（LAUNCHER_VERSION 1.1.0），与本机在用的 TCP 帧桥不是同一套，不要拿它改端口。
