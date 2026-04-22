# OpenStaff.Application.Contracts

## 项目用途
此项目定义 OpenStaff 的应用层公开边界，存放供 HTTP 控制器、前端客户端以及其他适配层使用的接口与 DTO，使调用方无需依赖应用实现或持久化细节。

## 架构定位
- 上游使用方：`OpenStaff.HttpApi`、测试代码，以及需要序列化请求/响应模型的客户端。
- 下游实现方：`OpenStaff.Application`。
- 不负责的内容：数据库访问、编排执行、供应商调用、HTTP 路由以及 SignalR 宿主。

## 关键命名空间与组件
- `AgentRoles`、`Agents`、`Projects`、`Sessions`、`Tasks`：核心业务流程对应的应用服务接口与请求/响应 DTO。
- `Files`：项目文件树、文件内容、差异和检查点相关契约。
- `Providers` 与 `Auth`：模型提供商账户管理契约，以及 GitHub 设备授权流程 DTO。
- `McpServers` 与 `Marketplace`：MCP 服务定义、配置、绑定，以及市场搜索/安装负载。
- `Monitor`、`ModelData`、`Settings`：运行监控、模型目录和系统设置契约。
- `Common\PagedResult<T>`：通用分页结果包装器。
- `OpenStaffApplicationContractsModule`：供模块化启动系统引用的模块标记。

## 重要依赖
- `OpenStaff.Core`：提供 DTO 使用的共享枚举、模型概念以及模块化基类。
- `OpenStaff.Plugins.ModelDataSource`：`ModelData` DTO 引用的模型目录类型。
- `OpenStaff.Provider.Abstractions`：让契约层与下游传输层使用的供应商/协议元数据保持一致。

## DI 与运行时职责
此程序集刻意保持几乎没有运行时行为。
- 它只发布 `IProjectAppService`、`ISessionAppService`、`IMcpServerAppService`、`IProviderAccountAppService`、`ISettingsAppService` 等接口。
- `OpenStaffApplicationContractsModule` 仅用于让其他模块声明对该契约面的依赖。
- 具体实现注册全部位于 `OpenStaff.Application`，不在此项目中完成。

## 与 HTTP / API 的关系
此项目中的 DTO 直接决定了 REST 层以及相关会话工具输出的结构，包括：
- 项目生命周期负载
- 会话、帧、事件与聊天消息负载
- 任务时间线与智能体事件流负载
- MCP 服务、配置与绑定负载
- 提供商账户、模型目录、监控和设置负载

此项目本身不提供控制器，也不承载 SignalR Hub。

## 构建命令
```powershell
dotnet build src\application\OpenStaff.Application.Contracts\OpenStaff.Application.Contracts.csproj
```

## 维护说明
- 暴露新的 HTTP 用例前，先在这里定义公开应用服务接口。
- 保持 DTO 面向传输、与实现解耦。
- 不要在此程序集加入数据库、编排或宿主逻辑。
