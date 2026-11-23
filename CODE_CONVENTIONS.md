# 客户端规范：AI 读取入口

版本：v2.0 · 2026-11-13。面向 Unity 竖屏休闲客户端的规范模板。

## 编码前必读

| 文件 | 内容 |
| --- | --- |
| [代码基础](docs/conventions/CODE_BASICS.md) | 正确性、职责、命名、接口、状态、资源与验证底线 |
| [项目基线](docs/conventions/PROJECT_BASELINE.md) | 适用工程、默认约束、已确认事实与待确认选型 |

两份基础文件是常驻最小规则。先查看实际工程，再按下表补读当前任务涉及的专题/小节；范围扩大时补读，已在上下文中的内容无需重复读取。纯文档任务只读本入口与待改文件。

## 按任务读取

| 任务涉及 | 专题 |
| --- | --- |
| 新模块、目录/程序集、跨模块依赖、设计模式或框架选型 | [架构与模式](docs/conventions/topics/architecture.md) |
| 复杂函数/接口、数值/时间/随机、集合、序列化或语言兼容 | [C# 详细规则](docs/conventions/topics/csharp.md) |
| Unity 生命周期、异步/协程、事件订阅、取消、对象池清理 | [生命周期与异步](docs/conventions/topics/lifecycle-async.md) |
| UI设计/布局/改版/换肤、UI素材及Review；面板/弹窗、绑定、列表、适配、文案与交互 | [UI工程专题](docs/conventions/topics/ui.md) + [UI排版与可读性规范](docs/UI排版与可读性规范.md) + [UI视觉样式规范](docs/UI视觉样式规范.md)（设计/实现/Review前必读） |
| schema/源表、配置生成、远程覆盖与校验 | [配置](docs/conventions/topics/config.md) |
| 图片分辨率、各模块图片总量、首包/下载预算 | [项目图片规格](docs/图片资源规格与首包预算.md) |
| ComfyUI 生图/选图、图片台账、首批与后续加载清单、正式入库 | [图片台账流程](art-source/README.md) |
| 资源命名/导入、Prefab/场景、图集、资源加载与分包 | [资源资产](docs/conventions/topics/assets.md) |
| 微信开发工具、WebGL 模块、SDK 安装与本地编译检查 | [微信本地环境](docs/微信小游戏本地环境搭建.md) |
| 每帧代码、GC、内存、渲染、启动耗时 | [性能](docs/conventions/topics/performance.md) |
| 配置/资源/代码更新方案、HybridCLR、旧包升级/恢复 | [更新系统](docs/conventions/topics/updates.md) |
| 网络、存档、领取/扣费、广告/内购、日志、凭证与调试 | [数据与安全](docs/conventions/topics/data-security.md) |
| 测试用例 | [测试要求](docs/conventions/topics/delivery.md#s-13-2) |
| Review、版本管理、提交、构建/发版、CI 门禁 | [验证与交付](docs/conventions/topics/delivery.md) |
| 术语、旧条款号、例外流程、历史变更、模板落地说明 | [参考](docs/conventions/reference.md) |

同一任务可命中多项。例如奖励按钮同时涉及 UI、异步、资产事务；仅改布局不需要读取支付或代码热更方案。专题保留旧条款号并提供文件链接，可先定位标题后读取相关段落，不要求展开全文。

## 维护方式

通用底线放“代码基础”，项目事实/决策放“项目基线”，细则只在所属专题维护。新增专题必须补充上表的触发条件；不要把专题全文复制到基础文件。

[archive/](docs/conventions/archive/) 仅保存旧版快照，按需追溯，不参与日常规则读取。未执行的检查和未确认的选型必须如实标注。
