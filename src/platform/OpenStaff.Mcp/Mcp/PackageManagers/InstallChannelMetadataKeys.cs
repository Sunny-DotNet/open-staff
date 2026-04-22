namespace OpenStaff.Mcp.PackageManagers;

/// <summary>
/// zh-CN: 约定的安装通道元数据键。
/// en: Conventional install-channel metadata keys.
/// </summary>
public static class InstallChannelMetadataKeys
{
    public const string EndpointUrl = "endpoint.url";
    public const string EndpointHeaders = "endpoint.headers";
    public const string RuntimeCommand = "runtime.command";
    public const string RuntimeArguments = "runtime.arguments";
    public const string RuntimeEnvironment = "runtime.environment";
    public const string RuntimeWorkingDirectory = "runtime.workingDirectory";
    public const string RuntimeCommandRelative = "runtime.commandRelativeToInstallDirectory";
    public const string RuntimeArgumentsRelativeIndexes = "runtime.argumentsRelativeToInstallDirectory";
}
