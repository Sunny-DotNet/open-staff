namespace OpenStaff.Core.Agents;

/// <summary>
/// 提示词加载器接口 / Prompt loader interface
/// </summary>
public interface IPromptLoader
{
    /// <summary>
    /// 加载系统提示词 / Load system prompt by resource name and language
    /// </summary>
    /// <param name="promptName">资源名，如 "communicator.system"</param>
    /// <param name="language">语言，如 "zh-Hans" 或 "en"</param>
    string Load(string promptName, string language);
}
