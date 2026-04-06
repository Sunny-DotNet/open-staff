using OpenStaff.Core.Modularity;
using OpenStaff.Provider;

namespace OpenStaff.Provider.Tests;

/// <summary>
/// 测试启动模块 — 聚合所有 Provider 模块
/// </summary>
[DependsOn(
    typeof(OpenStaffProviderOpenAIModule),
    typeof(OpenStaffProviderAnthropicModule),
    typeof(OpenStaffProviderGoogleModule),
    typeof(OpenStaffProviderNewApiModule),
    typeof(OpenStaffProviderGitHubCopilotModule))]
public class ProviderTestModule : OpenStaffModule;
