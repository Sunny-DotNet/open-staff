# OpenStaff.AppHost

## 用途与职责
`OpenStaff.AppHost` 是该方案的 .NET Aspire 开发宿主，职责是为本地开发组合并启动可运行的应用图，而不是提供可复用的通用类库能力。

## 架构位置
- 目标框架：`net10.0`
- 可执行 Aspire 宿主（`IsAspireHost=true`）
- 本地开发场景下的顶层编排入口
- 负责协调后端与前端进程

## 关键组件
- `Program.cs`
  - `DistributedApplication.CreateBuilder(args)` 创建 Aspire 应用模型
  - `AddProject<Projects.OpenStaff_HttpApi_Host>("api")` 注册后端 API 项目
  - `AddViteApp("web", "../../../web/apps/web-antd")` 直接从重建后的单 app 目录启动 Ant Design Vue 前端
  - `.WithReference(api)` 为前端关联 API 的依赖信息与服务发现元数据
  - `.WithEnvironment("VITE_OPENSTAFF_PROXY_TARGET", api.GetEndpoint("http"))` 注入供 Vite 代理使用的后端地址
  - `.WithEndpoint("http", e => { e.Port = 5666; e.IsProxied = false; })` 将前端开发服务器固定暴露在 `5666` 端口
  - `.WithExternalHttpEndpoints()` 让 Vite 端点对 Aspire 内部网络之外可见
  - `.WaitFor(api)` 保证前端在 API 就绪后再启动

## 重要依赖
- `Aspire.AppHost.Sdk` `13.2.1`
- `Aspire.Hosting.AppHost`
- 项目引用：`..\OpenStaff.HttpApi.Host\OpenStaff.HttpApi.Host.csproj`

## 运行时与宿主行为
- 该宿主会在本地开发时同时启动 API 项目和 Vite 前端。
- 它不会额外创建数据库容器；当前方案依赖 API 与基础设施层默认的 SQLite 行为。
- 前端从 `web\apps\web-antd` 启动，Aspire 会注入 `VITE_PORT=5666` 和后端代理目标。
- 因此仍然要求在启动 AppHost 之前先完成 `pnpm install`。
- 前端端口显式设为 `5666`，且不经过代理。
- 作为 Aspire 宿主，它的仪表盘和配套端点遵循 Aspire 的常规本地开发行为。

## 构建与运行命令
以下命令默认在仓库根目录执行：

```powershell
cd web
pnpm install
cd ..
dotnet build src\hosts\OpenStaff.AppHost\OpenStaff.AppHost.csproj
dotnet run --project src\hosts\OpenStaff.AppHost\OpenStaff.AppHost.csproj
```

如果只需要后端进程，可以改为执行 `dotnet run --project src\hosts\OpenStaff.HttpApi.Host\OpenStaff.HttpApi.Host.csproj`。
