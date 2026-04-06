# 系统依赖

OpenStaff 运行需要以下系统级依赖。

## .NET SDK

后端基于 .NET 10 构建。

- **版本要求**: .NET SDK 10.0+
- **安装**: https://dotnet.microsoft.com/download/dotnet/10.0

```bash
# 验证
dotnet --version
```

## Node.js

前端构建和 MCP stdio 服务器（如 `npx` 启动的服务）依赖 Node.js。

- **版本要求**: ^20.19.0 || ^22.18.0 || ^24.0.0
- **安装**: https://nodejs.org/

```bash
# 验证
node --version
npx --version
```

## pnpm

前端包管理器。

- **版本要求**: >= 10.0.0
- **安装**: `corepack enable && corepack prepare pnpm@latest --activate`（或 `npm install -g pnpm`）
- **文档**: https://pnpm.io/installation

```bash
# 验证
pnpm --version
```

## Python

部分 MCP 服务器（如 `mcp-server-fetch`）通过 `uvx` 运行，依赖 Python 环境。

- **版本要求**: >= 3.10
- **安装**: https://www.python.org/downloads/

```bash
# 验证
python --version
```

## uv / uvx

[uv](https://github.com/astral-sh/uv) 是高性能 Python 包管理工具，`uvx` 是其内置的一次性运行命令（类似 `npx`）。MCP 市场中 Python 类型的服务器使用 `uvx` 启动。

- **安装方式**（任选其一）:

```bash
# Windows (PowerShell)
irm https://astral.sh/uv/install.ps1 | iex

# Windows (winget)
winget install astral-sh.uv

# macOS / Linux
curl -LsSf https://astral.sh/uv/install.sh | sh
```

- **文档**: https://docs.astral.sh/uv/

```bash
# 验证
uv --version
uvx --version
```

## Docker（可选）

用于一键部署完整环境（PostgreSQL + API + 前端）。

- **安装**: https://docs.docker.com/get-docker/

```bash
# 验证
docker --version
docker compose version
```

## 快速检查脚本

```powershell
# PowerShell — 一键检查所有依赖
@("dotnet", "node", "npx", "pnpm", "python", "uv", "uvx") | ForEach-Object {
    $cmd = $_
    try {
        $ver = & $cmd --version 2>&1 | Select-Object -First 1
        Write-Host "✅ $cmd : $ver"
    } catch {
        Write-Host "❌ $cmd : 未安装"
    }
}
```
