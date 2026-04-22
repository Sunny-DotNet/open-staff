using OpenStaff.Core.Modularity;
using OpenStaff.Plugin.Anthropic;
using OpenStaff.Plugin.GitHubCopilot;
using OpenStaff.Plugin.Google;
using OpenStaff.Plugin.NewApi;
using OpenStaff.Plugin.OpenAI;

namespace OpenStaff.Provider.Tests;

/// <summary>
/// 测试启动模块 — 聚合所有 Provider 模块
/// </summary>
[DependsOn(
    typeof(OpenStaffPluginOpenAIModule),
    typeof(OpenStaffPluginAnthropicModule),
    typeof(OpenStaffPluginGoogleModule),
    typeof(OpenStaffPluginNewApiModule),
    typeof(OpenStaffPluginGitHubCopilotModule))]
public class ProviderTestModule : OpenStaffModule;
