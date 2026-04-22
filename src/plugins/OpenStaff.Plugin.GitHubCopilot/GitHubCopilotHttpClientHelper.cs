using Google.GenAI;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenStaff;

internal class GitHubCopilotHttpClientHelper
{
    public static void ConfigureHttpClient(HttpClient httpClient)
    {
        // GitHub Copilot API 要求使用特定的 User-Agent 以识别请求来源。
        httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("GitHubCopilotChat/1.0.102");
        httpClient.DefaultRequestHeaders.Add("Editor-Version", "vscode/1.100.0");
        httpClient.DefaultRequestHeaders.Add("Editor-Plugin-Version", "copilot-chat/0.27.0");
        httpClient.DefaultRequestHeaders.Add("Copilot-Integration-Id", "vscode-chat");
    }
}
