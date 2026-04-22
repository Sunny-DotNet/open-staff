# OpenStaff.HttpApi

## 项目用途
此项目是 OpenStaff 的 HTTP 传输层，提供一组薄控制器，把外部 HTTP 请求转换为对 `OpenStaff.Application.Contracts` 接口的调用。

## 架构定位
- 由 `OpenStaff.HttpApi.Host` 宿主加载到 Web 应用中。
- 依赖契约程序集，而不是应用实现或持久化实现。
- 位于外部 HTTP 客户端与应用层之间。
- 不负责 SignalR 宿主、启动编排、数据库访问或智能体执行。

## 关键命名空间与组件
- `OpenStaffHttpApiModule`：注册本程序集中的控制器，并配置 JSON 循环引用处理。
- `ProjectsController`：项目增删改查、工作区初始化、启动、README 获取、导出和导入。
- `SessionsController`：会话创建、发消息、事件查询、帧消息查询、取消、弹栈、按场景查询活跃会话以及聊天消息分页。
- `TasksController`：任务增删改查、恢复阻塞任务和任务时间线查询。
- `AgentsController` 与 `AgentRolesController`：项目智能体分配、事件流、定向消息、角色管理、厂商模型查询和测试对话。
- `ProviderAccountsController`：提供商账户增删改查、模型列表以及 GitHub 设备授权流程。
- `McpServersController` 与 `MarketplaceController`：MCP 定义、配置、绑定，以及市场来源、搜索和安装接口。
- `FilesController`：工作区文件树、文件内容、差异和检查点接口。
- `MonitorController`、`ModelDataController`、`SettingsController`、`ProtocolsController`：健康与统计、模型目录、系统设置和提供商协议元数据接口。

## 重要依赖
- `OpenStaff.Application.Contracts`：所有控制器注入的应用服务接口与 DTO。
- `Microsoft.AspNetCore.App`：MVC 基类、路由、模型绑定和 JSON 配置能力。
- 通过传递依赖引入的 provider/protocol 抽象被 `ProtocolsController` 用来输出协议信息。

## DI 与运行时职责
`OpenStaffHttpApiModule` 故意保持精简。
- 调用 `AddControllers()`。
- 通过 `AddApplicationPart(typeof(OpenStaffHttpApiModule).Assembly)` 加载本程序集。
- 为 JSON 序列化设置 `ReferenceHandler.IgnoreCycles`。
- 具体服务注册交给 `OpenStaff.Application`，宿主级职责交给 `OpenStaff.HttpApi.Host`。

## HTTP 行为
- 路由按资源族统一组织在 `/api/...` 下。
- 控制器会把应用层结果转换成 `CreatedAtAction`、`Ok`、`NoContent`、`NotFound` 与 `BadRequest` 等响应。
- 部分接口会把上游提供商失败转换为 `502 Bad Gateway`，尤其是提供商模型列表和 GitHub 设备授权相关接口。
- 实时流式能力不在此项目实现；SignalR 端点位于 `OpenStaff.HttpApi.Host`。

## 构建与运行
```powershell
dotnet build src\application\OpenStaff.HttpApi\OpenStaff.HttpApi.csproj
dotnet run --project src\hosts\OpenStaff.HttpApi.Host
```

## 维护说明
- 保持控制器足够薄；新增用例应先扩展契约，再由应用层实现。
- 不要在此程序集加入持久化或编排逻辑。
