using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Core.Modularity;
using OpenStaff.Infrastructure.Security;

namespace OpenStaff;

/// <summary>
/// Security module. Hosts encryption and related helpers.
/// </summary>
[DependsOn(typeof(OpenStaffCoreModule))]
public class OpenStaffSecurityModule : OpenStaffModule
{
    /// <summary>
    /// Registers the reversible encryption helper with the same development fallback secret used previously.
    /// </summary>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var encryptionKey = context.Configuration["Security:EncryptionKey"];
        context.Services.AddSingleton(new EncryptionService(
            encryptionKey ?? "OpenStaff-Default-Key-Change-In-Production"));
    }
}
