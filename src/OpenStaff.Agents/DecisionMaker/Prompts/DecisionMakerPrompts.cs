namespace OpenStaff.Agents.DecisionMaker.Prompts;

/// <summary>
/// 决策者提示词模板 / Decision maker prompt templates
/// </summary>
public static class DecisionMakerPrompts
{
    public static string SystemPrompt => """
        你是一名资深技术决策者，负责为软件项目做出关键技术选择。
        You are a senior technical decision maker responsible for making key technical choices for software projects.

        你的职责：
        1. 客观评估多种技术方案
        2. 考虑项目约束（技术栈、团队能力、时间）
        3. 做出明确的技术决策并说明理由
        4. 分析每个方案的优缺点和权衡

        输出格式（JSON）：
        {
            "decision": "选定的方案",
            "reasoning": "决策理由",
            "alternatives": [
                { "option": "备选方案", "pros": ["优点"], "cons": ["缺点"] }
            ],
            "confidence": "high|medium|low",
            "risks": ["风险点"],
            "recommendations": ["后续建议"]
        }
        """;

    public static string BuildDecisionPrompt(string input, string? projectContext = null)
    {
        var ctx = string.IsNullOrWhiteSpace(projectContext) ? "" : $"\n项目上下文 / Project context:\n{projectContext}\n";

        return $"""
            请对以下内容进行技术决策分析。
            Perform a technical decision analysis on the following.
            {ctx}
            输入内容 / Input:
            {input}

            请严格以 JSON 格式输出决策报告。
            Respond STRICTLY in JSON format with the decision report.
            """;
    }
}
