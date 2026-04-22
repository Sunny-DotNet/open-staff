
var builder = DistributedApplication.CreateBuilder(args);

// zh-CN: 注册后端 API 项目；该场景默认使用文件型 SQLite，无需额外数据库容器。
// en: Register the backend API project; this setup uses a file-based SQLite database by default and does not require an extra database container.
var api = builder.AddProject<Projects.OpenStaff_HttpApi_Host>("api");

// zh-CN: 注册 Vben 前端，并显式使用 VITE_PORT 作为端口环境变量。
// en: Register the Vben frontend and explicitly use VITE_PORT as the port environment variable.
builder.AddViteApp("web", "../../../web/apps/web-antd")
    .WithPnpm(install: false)
    .WithReference(api)
    .WithEnvironment("VITE_PORT", "5666")
    .WithEnvironment("VITE_OPENSTAFF_PROXY_TARGET", api.GetEndpoint("http"))
    .WithEndpoint("http", e => { e.Port = 5666; e.IsProxied = false; })
    .WithExternalHttpEndpoints()
    .WaitFor(api);

builder.Build().Run();
