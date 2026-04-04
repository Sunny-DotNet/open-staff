var builder = DistributedApplication.CreateBuilder(args);

// 后端 API (SQLite 文件数据库，不需要外部数据库服务)
// Backend API (SQLite file-based database, no external DB service needed)
var api = builder.AddProject<Projects.OpenStaff_Api>("api");

// 前端 Vben / Frontend Vben App
// Vben 框架读 VITE_PORT（非默认的 PORT），通过第三个参数指定端口环境变量名
builder.AddViteApp("web", "../../web/apps/web-antd", "VITE_PORT")
    .WithPnpm()
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
