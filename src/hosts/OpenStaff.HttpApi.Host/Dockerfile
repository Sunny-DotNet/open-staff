# ============================================================
# OpenStaff API — 多阶段 Dockerfile
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 复制解决方案和项目文件，先还原依赖
COPY OpenStaff.slnx .
COPY OpenStaff.Core/OpenStaff.Core.csproj OpenStaff.Core/
COPY OpenStaff.Agents/OpenStaff.Agents.csproj OpenStaff.Agents/
COPY OpenStaff.Infrastructure/OpenStaff.Infrastructure.csproj OpenStaff.Infrastructure/
COPY OpenStaff.Api/OpenStaff.Api.csproj OpenStaff.Api/
RUN dotnet restore OpenStaff.slnx

# 复制全部源码并发布
COPY . .
RUN dotnet publish OpenStaff.Api/OpenStaff.Api.csproj -c Release -o /app/publish --no-restore

# ---- 运行镜像 ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# 安装 Git（生产者角色需要）
RUN apt-get update && apt-get install -y --no-install-recommends git && rm -rf /var/lib/apt/lists/*

# 创建工作目录
RUN mkdir -p /app/workspaces /app/plugins

COPY --from=build /app/publish .

EXPOSE 5000
ENTRYPOINT ["dotnet", "OpenStaff.Api.dll"]
