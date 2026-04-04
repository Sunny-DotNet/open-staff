var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL 数据库 / PostgreSQL database
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("openstaff-pgdata");

var db = postgres.AddDatabase("openstaff");

// 后端 API / Backend API
var api = builder.AddProject<Projects.OpenStaff_Api>("api")
    .WithReference(db)
    .WaitFor(db);

// 前端 Vben / Frontend Vben App
builder.AddViteApp("web", "../../web/apps/web-antd")
    .WithReference(api)
    .WithHttpEndpoint(port: 3000, env: "PORT");

builder.Build().Run();
