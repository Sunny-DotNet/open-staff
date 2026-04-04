var builder = DistributedApplication.CreateBuilder(args);

// 后端 API (SQLite 文件数据库，不需要外部数据库服务)
// Backend API (SQLite file-based database, no external DB service needed)
var api = builder.AddProject<Projects.OpenStaff_Api>("api");

// 前端 Vben / Frontend Vben App
builder.AddViteApp("web", "../../web/apps/web-antd")
    .WithPnpm()
    .WithReference(api)
    .WithEnvironment("PORT", "3000")
    .WaitFor(api);

builder.Build().Run();
