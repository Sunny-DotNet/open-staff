namespace OpenStaff.Agents.Producer.Prompts;

/// <summary>
/// 生产者提示词模板 / Producer prompt templates
/// </summary>
public static class ProducerPrompts
{
    /// <summary>
    /// 生产者系统提示词 / Producer system prompt
    /// </summary>
    public static string SystemPrompt => """
        你是一名专业的软件开发工程师，擅长编写整洁、结构良好的代码。
        You are an expert software developer who writes clean, well-structured code.

        你的职责 / Your responsibilities:
        1. 根据任务描述生成高质量代码 / Generate high-quality code based on task descriptions
        2. 使用工具调用来创建和编辑文件 / Use tool calls to create and edit files
        3. 遵循项目约定和最佳实践 / Follow project conventions and best practices
        4. 添加适当的注释 / Include appropriate comments
        5. 确保代码可编译和运行 / Ensure code compiles and runs

        可用工具 / Available tools:
        - create_file: 创建新文件 / Create a new file
        - edit_file: 编辑现有文件 / Edit an existing file
        - read_file: 读取文件内容 / Read file content
        - list_files: 列出目录内容 / List directory contents

        工作规范 / Work guidelines:
        - 所有文件路径相对于项目工作空间根目录 / All file paths are relative to the project workspace root
        - 创建文件前先检查是否已存在 / Check if a file exists before creating it
        - 编辑文件时提供精确的旧内容和新内容 / Provide exact old and new content when editing
        - 每次修改后说明变更理由 / Explain the reason for each change
        """;

    /// <summary>
    /// 构建编码提示词 / Build coding prompt
    /// </summary>
    /// <param name="taskTitle">任务标题 / Task title</param>
    /// <param name="taskDescription">任务描述 / Task description</param>
    /// <param name="projectContext">项目上下文 / Project context</param>
    /// <param name="existingFiles">已有文件列表 / Existing file list</param>
    /// <returns>格式化后的提示词 / Formatted prompt</returns>
    public static string BuildCodingPrompt(
        string taskTitle,
        string taskDescription,
        string? projectContext = null,
        string? existingFiles = null)
    {
        var contextSection = string.IsNullOrWhiteSpace(projectContext)
            ? ""
            : $"""

                项目上下文 / Project context:
                {projectContext}
                """;

        var filesSection = string.IsNullOrWhiteSpace(existingFiles)
            ? ""
            : $"""

                项目现有文件 / Existing project files:
                {existingFiles}
                """;

        return $"""
            请根据以下任务要求进行编码实现。
            Implement the following task by writing code.
            {contextSection}{filesSection}

            任务标题 / Task title: {taskTitle}

            任务描述 / Task description:
            {taskDescription}

            请使用工具调用来创建或编辑文件，完成后说明你做了哪些变更。
            Use tool calls to create or edit files. After completion, summarize what changes you made.
            """;
    }

    /// <summary>
    /// 构建代码审查修复提示词 / Build code review fix prompt
    /// </summary>
    /// <param name="reviewFeedback">审查反馈 / Review feedback</param>
    /// <param name="originalTask">原始任务描述 / Original task description</param>
    /// <returns>格式化后的提示词 / Formatted prompt</returns>
    public static string BuildReviewFixPrompt(string reviewFeedback, string originalTask)
    {
        return $"""
            之前的代码实现收到了审查反馈，请根据反馈进行修正。
            The previous code implementation received review feedback. Please fix accordingly.

            原始任务 / Original task:
            {originalTask}

            审查反馈 / Review feedback:
            {reviewFeedback}

            请使用工具调用来修改相关文件。
            Use tool calls to modify the relevant files.
            """;
    }
}
