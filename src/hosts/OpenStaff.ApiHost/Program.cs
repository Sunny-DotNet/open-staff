using Microsoft.Extensions.Options;
using OpenStaff.ApiHost;
using OpenStaff.Core.Modularity;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.Options;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenStaffModules<OpenStaffApiHostModule>(builder.Configuration);

var app = builder.Build();

{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var openStaffOptions = scope.ServiceProvider.GetRequiredService<IOptions<OpenStaffOptions>>().Value;
    await ProviderAccountEnvConfigBackfill.BackfillAsync(db, openStaffOptions);
}

app.Services.UseOpenStaffModules();
app.MapDefaultEndpoints();
app.MapControllers();

app.Run();
