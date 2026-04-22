# OpenStaff.ServiceDefaults

## 用途与职责
`OpenStaff.ServiceDefaults` 是仓库内服务宿主共用的 .NET Aspire 启动层项目，用来集中配置 OpenTelemetry、健康检查、服务发现以及具备弹性的 `HttpClient` 默认策略。

## 架构位置
- 目标框架：`net10.0`
- 共享项目（`IsAspireSharedProject=true`）
- 面向可执行服务应用复用，不承担业务逻辑职责
- 所处层级是宿主与启动层，而不是领域层或基础设施层

## 关键命名空间与组件
- `Microsoft.Extensions.Hosting.Extensions`
  - `AddServiceDefaults<TBuilder>()`
    - 一次性接入遥测、健康检查、服务发现和默认 `HttpClient` 策略
  - `ConfigureOpenTelemetry<TBuilder>()`
    - 配置 OpenTelemetry 日志、指标与链路追踪
  - `AddDefaultHealthChecks<TBuilder>()`
    - 注册带有 `live` 标记的 `self` 健康检查
  - `MapDefaultEndpoints(WebApplication)`
    - 在开发环境映射 `/health` 与 `/alive`

## 重要依赖
- 框架引用：`Microsoft.AspNetCore.App`
- `Microsoft.Extensions.Http.Resilience`
- `Microsoft.Extensions.ServiceDiscovery`
- `OpenTelemetry.Exporter.OpenTelemetryProtocol`
- `OpenTelemetry.Extensions.Hosting`
- `OpenTelemetry.Instrumentation.AspNetCore`
- `OpenTelemetry.Instrumentation.Http`
- `OpenTelemetry.Instrumentation.Runtime`

## 运行时与宿主行为
- `AddServiceDefaults()` 会启用服务发现，并为 `HttpClient` 配置标准弹性处理器和服务发现能力。
- `ConfigureOpenTelemetry()` 会采集 ASP.NET Core、`HttpClient` 和运行时的指标与链路，同时打开带格式日志消息和作用域信息。
- 只有在配置了 `OTEL_EXPORTER_OTLP_ENDPOINT` 时，才会启用 OTLP 导出。
- `MapDefaultEndpoints()` 仅在开发环境暴露 `/health` 和 `/alive`，避免默认把这些端点带到生产环境。
- 扩展方法放在 `Microsoft.Extensions.Hosting` 命名空间中，方便宿主用常规启动代码直接接入。
- 该项目本身没有独立进程，只有被其他应用调用这些扩展方法时才会体现其运行时效果。

## 构建与使用命令
以下命令在仓库根目录执行：

```powershell
dotnet build src\hosts\OpenStaff.ServiceDefaults\OpenStaff.ServiceDefaults.csproj
dotnet run --project src\hosts\OpenStaff.HttpApi.Host\OpenStaff.HttpApi.Host.csproj
```

第二条命令用于在真实宿主中观察这些默认配置，因为该项目本身不能单独运行。
