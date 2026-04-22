using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Configuration;

namespace OpenStaff.Core.Modularity;

internal static class StartupPluginModuleDiscovery
{
    private const string PluginAssemblyPattern = "OpenStaff.Plugin.*.dll";

    public static IReadOnlyList<Type> Discover(IConfiguration configuration)
    {
        var assemblies = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            TryAddPluginAssembly(assembly, assemblies);

        foreach (var assemblyPath in EnumeratePluginAssemblyPaths(configuration))
        {
            var assembly = LoadAssembly(assemblyPath);
            TryAddPluginAssembly(assembly, assemblies);
        }

        return assemblies.Values
            .SelectMany(GetStartupPluginModuleTypes)
            .Distinct()
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();
    }

    private static IEnumerable<string> EnumeratePluginAssemblyPaths(IConfiguration configuration)
    {
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFullPath(AppContext.BaseDirectory)
        };

        foreach (var directory in GetConfiguredPluginDirectories(configuration))
            directories.Add(directory);

        foreach (var directory in directories)
        {
            if (!Directory.Exists(directory))
                continue;

            foreach (var assemblyPath in Directory.EnumerateFiles(directory, PluginAssemblyPattern, SearchOption.TopDirectoryOnly))
                yield return Path.GetFullPath(assemblyPath);
        }
    }

    private static IEnumerable<string> GetConfiguredPluginDirectories(IConfiguration configuration)
    {
        var configuredDirectories = configuration
            .GetSection("OpenStaff:PluginDirectories")
            .GetChildren()
            .Select(section => section.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => Path.GetFullPath(value!))
            .ToList();

        foreach (var directory in configuredDirectories)
            yield return directory;

        var workingDirectory = configuration["OpenStaff:WorkingDirectory"];
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            workingDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".staff");
        }

        yield return Path.GetFullPath(Path.Combine(workingDirectory, "plugins"));
    }

    private static Assembly LoadAssembly(string assemblyPath)
    {
        try
        {
            var assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
            var loadedAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => AssemblyName.ReferenceMatchesDefinition(assembly.GetName(), assemblyName));

            return loadedAssembly ?? AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to load startup plugin assembly '{assemblyPath}'.",
                ex);
        }
    }

    private static void TryAddPluginAssembly(Assembly assembly, Dictionary<string, Assembly> assemblies)
    {
        var assemblyName = assembly.GetName().Name;
        if (string.IsNullOrWhiteSpace(assemblyName)
            || !assemblyName.StartsWith("OpenStaff.Plugin.", StringComparison.Ordinal))
        {
            return;
        }

        assemblies.TryAdd(assemblyName, assembly);
    }

    private static IEnumerable<Type> GetStartupPluginModuleTypes(Assembly assembly)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            throw new InvalidOperationException(
                $"Failed to inspect startup plugin assembly '{assembly.FullName}'.",
                ex);
        }

        return types.Where(type =>
            !type.IsAbstract
            && typeof(OpenStaffModule).IsAssignableFrom(type)
            && type.GetCustomAttribute<StartupPluginModuleAttribute>() != null);
    }
}
