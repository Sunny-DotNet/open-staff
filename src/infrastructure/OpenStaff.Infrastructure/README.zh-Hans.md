# OpenStaff.Infrastructure

## 用途与职责
`OpenStaff.Infrastructure` 为核心层契约提供具体实现，负责持久化、Git 集成、敏感配置可逆加密、项目导入导出以及插件发现与加载。

## 架构位置
- 目标框架：`net10.0`
- 依赖 `OpenStaff.Core`
- 位于应用层与 SQLite 文件、Git 仓库、归档文件等外部资源之间
- 通过 `OpenStaffInfrastructureModule` 暴露统一注册入口

## 关键命名空间与组件
- `OpenStaff.Infrastructure.Persistence`
  - `AppDbContext`
  - EF Core 实体配置
  - `Migrations\`
  - 设计时工厂 `AppDbContextFactory`
  - 持久化项目、会话、执行帧、消息、事件、检查点、供应商账号、MCP 配置等数据
- `OpenStaff.Infrastructure.Git`
  - `GitService`
  - 基于 LibGit2Sharp 提供仓库初始化、提交、差异读取和历史查询
- `OpenStaff.Infrastructure.Security`
  - `EncryptionService`
  - 使用 AES-256，并通过 SHA-256 派生固定长度密钥；密文中前置保存 IV
- `OpenStaff.Infrastructure.Export`
  - `ProjectExporter`
  - `ProjectImporter`
  - 打包和恢复包含数据库快照与工作区文件的 `.openstaff` 归档
- `OpenStaff.Infrastructure.Plugins`
  - `PluginLoader`
  - 扫描插件 DLL，并初始化其中的 `IPlugin` 实现
- `OpenStaffInfrastructureModule`
  - 注册 DbContext、`HttpClient`、Git、导入导出、插件与加密服务

## 重要依赖
- 项目引用：`..\OpenStaff.Core\OpenStaff.Core.csproj`
- `Microsoft.EntityFrameworkCore.Sqlite`
- `Microsoft.EntityFrameworkCore.Design`
- `Microsoft.Extensions.Http`
- `LibGit2Sharp`

## 运行时与宿主行为
- `OpenStaffInfrastructureModule` 依赖 `OpenStaffCoreModule`。
- 当前实现始终使用 EF Core 的 SQLite 提供程序。
- 连接字符串优先读取 `ConnectionStrings:openstaff` 或 `ConnectionStrings:DefaultConnection`；若未提供，则回退到 `%USERPROFILE%\.staff\openstaff.db`。
- `EncryptionService` 从 `Security:EncryptionKey` 读取密钥；缺失时会使用仅适合开发环境的兜底密钥，正式部署必须覆盖。
- `AppDbContextFactory` 让 `dotnet ef` 默认连接到与运行时一致的本地 SQLite 文件。
- `ProjectImporter` 在解压工作区文件前会校验目标路径，避免导入内容越界写入工作区之外。
- 该项目不是可执行宿主，通常由 `OpenStaff.HttpApi.Host` 等上层应用承载运行。

## 构建与操作命令
以下命令在仓库根目录执行：

```powershell
dotnet build src\infrastructure\OpenStaff.Infrastructure\OpenStaff.Infrastructure.csproj
dotnet ef migrations list --project src\infrastructure\OpenStaff.Infrastructure\OpenStaff.Infrastructure.csproj
dotnet ef database update --project src\infrastructure\OpenStaff.Infrastructure\OpenStaff.Infrastructure.csproj
dotnet run --project src\hosts\OpenStaff.HttpApi.Host\OpenStaff.HttpApi.Host.csproj
```

其中最后一条命令是验证该项目运行效果的实际入口。
