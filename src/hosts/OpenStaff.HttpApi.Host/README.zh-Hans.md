# OpenStaff.HttpApi.Host

## 项目用途
此项目是 OpenStaff 的 ASP.NET Core 宿主进程，负责启动模块化应用、执行数据库迁移、初始化模型数据源，并对外暴露 UI 使用的 REST 与 SignalR 接口面。

## 架构定位
- 本地开发与部署时的入口可执行项目。
- 负责组合 `OpenStaff.Application`、`OpenStaff.HttpApi` 与 `OpenStaff.ServiceDefaults`。
- 承担中间件、CORS、OpenAPI、SignalR、配置文件与启动任务等宿主职责。

## 关键命名空间与组件
- `Program.cs`：创建 Web 应用、加载模块、执行迁移、初始化模型目录，并映射中间件与端点。
- `OpenStaffHttpApiHostModule`：注册 SignalR、OpenAPI、默认 CORS 策略以及 `INotificationService`。
- `Hubs\NotificationHub`：系统唯一的 SignalR Hub，负责频道加入/离开和会话事件流。
- `Services\NotificationService`：把 `INotificationService` 桥接到 SignalR 分组与内存会话流管理器。
- `Middleware\ErrorHandlingMiddleware`：把常见异常转换为 JSON 错误响应。
- `Middleware\LocaleMiddleware`：根据数据库设置、`Accept-Language` 或服务器区域性解析请求语言。
- `appsettings.json` 与 `appsettings.Development.json`：宿主配置文件，包括 CORS 来源。
- `OpenStaff.HttpApi.Host.http`：手工调试 HTTP 请求的示例文件。
- `Dockerfile`：宿主进程的容器打包入口。

## 重要依赖
- `OpenStaff.Application`：应用服务、编排、会话运行时与提供商集成。
- `OpenStaff.HttpApi`：被宿主加载的 MVC 控制器程序集。
- `OpenStaff.ServiceDefaults`：Aspire 的遥测、服务发现和健康检查默认配置。
- `Microsoft.AspNetCore.OpenApi` 与 `Scalar.AspNetCore`：开发环境下的 API 描述与参考界面。
- `Microsoft.EntityFrameworkCore.Design`：EF Core 工具支持。
- `System.Reactive`：与流式基础设施共享使用。

## DI 与运行时职责
- `OpenStaffHttpApiHostModule` 调用 `AddSignalR()`、`AddOpenApi()`，并注册默认 CORS 策略和 `INotificationService`。
- 默认 CORS 策略读取 `Cors:Origins`，缺省回退到 `http://localhost:3000`。
- `Program.cs` 通过 `AddOpenStaffModules<OpenStaffHttpApiHostModule>()` 与 `UseOpenStaffModules()` 启动模块注册和初始化逻辑。
- 启动时会自动对 `AppDbContext` 执行迁移。
- 启动时还会初始化 `IModelDataSource`，确保模型刷新与查询接口立即可用。

## HTTP、SignalR 与 API 行为
- REST 控制器来自 `OpenStaff.HttpApi`，通过 `app.MapControllers()` 暴露。
- 全系统唯一的 SignalR Hub 路径为 `/hubs/notification`。
- `NotificationHub.StreamSession(sessionId)` 会持续输出 `SessionEvent`，并把调用方加入 `session:{id}` 分组。
- `NotificationService` 还会向项目分组和会话分组推送频道通知。
- OpenAPI 与 Scalar 参考界面仅在开发环境启用。
- `MapDefaultEndpoints()` 会暴露 Aspire 的健康检查与诊断端点。

## 构建与运行
```powershell
dotnet build src\hosts\OpenStaff.HttpApi.Host\OpenStaff.HttpApi.Host.csproj
dotnet run --project src\hosts\OpenStaff.HttpApi.Host
```

## 维护说明
- 业务逻辑应保留在 `OpenStaff.Application`；此项目应聚焦宿主与跨传输层关注点。
- 除非实时架构明确调整，否则应保持单 Hub 设计。
