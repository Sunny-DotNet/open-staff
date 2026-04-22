namespace OpenStaff.Core.Modularity;

/// <summary>
/// Marks an <see cref="OpenStaffModule"/> as a startup plugin module that can be discovered
/// from referenced assemblies or plugin directories before the service provider is built.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class StartupPluginModuleAttribute : Attribute
{
}
