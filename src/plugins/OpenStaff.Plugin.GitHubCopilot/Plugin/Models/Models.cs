using System.Text.Json.Serialization;

namespace OpenStaff.Plugin.Models;

/// <summary>
/// GitHub Copilot 模型列表响应。
/// GitHub Copilot model list response.
/// </summary>
/// <param name="Data">
/// 模型数据集合。
/// Collection of model entries.
/// </param>
/// <param name="Object">
/// 响应对象类型。
/// Response object type.
/// </param>
public record struct GitHubCopilotModelListResponse(
    [property: JsonPropertyName("data")] List<GitHubCopilotModelData> Data,
    [property: JsonPropertyName("object")] string Object);

/// <summary>
/// GitHub Copilot 模型条目。
/// GitHub Copilot model entry.
/// </summary>
/// <param name="Id">
/// 模型唯一标识。
/// Unique model identifier.
/// </param>
/// <param name="Name">
/// 模型显示名称。
/// Human-readable model name.
/// </param>
/// <param name="Object">
/// API 对象类型。
/// API object type.
/// </param>
/// <param name="Vendor">
/// 模型供应商。
/// Model vendor.
/// </param>
/// <param name="Version">
/// 模型版本。
/// Model version.
/// </param>
/// <param name="ModelPickerCategory">
/// 模型选择器分类。
/// Model picker category.
/// </param>
/// <param name="ModelPickerEnabled">
/// 是否在模型选择器中展示。
/// Whether the model is shown in the model picker.
/// </param>
/// <param name="Preview">
/// 是否为预览模型。
/// Whether the model is in preview.
/// </param>
/// <param name="Capabilities">
/// 模型能力信息。
/// Model capabilities.
/// </param>
/// <param name="Policy">
/// 模型策略信息。
/// Model policy information.
/// </param>
/// <param name="SupportedEndpoints">
/// 支持的 API 端点列表。
/// List of supported API endpoints.
/// </param>
public record struct GitHubCopilotModelData(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("object")] string Object,
    [property: JsonPropertyName("vendor")] string Vendor,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("model_picker_category")] string? ModelPickerCategory,
    [property: JsonPropertyName("model_picker_enabled")] bool ModelPickerEnabled,
    [property: JsonPropertyName("preview")] bool Preview,
    [property: JsonPropertyName("capabilities")] GitHubCopilotModelCapabilities Capabilities,
    [property: JsonPropertyName("policy")] GitHubCopilotModelPolicy? Policy,
    [property: JsonPropertyName("supported_endpoints")] List<string>? SupportedEndpoints);

/// <summary>
/// GitHub Copilot 模型能力描述。
/// GitHub Copilot model capability description.
/// </summary>
/// <param name="Family">
/// 模型家族名称。
/// Model family name.
/// </param>
/// <param name="Type">
/// 模型类型。
/// Model type.
/// </param>
/// <param name="Tokenizer">
/// 分词器名称。
/// Tokenizer name.
/// </param>
/// <param name="Limits">
/// 模型限制信息。
/// Model limit information.
/// </param>
/// <param name="Supports">
/// 功能支持矩阵。
/// Supported feature matrix.
/// </param>
public record struct GitHubCopilotModelCapabilities(
    [property: JsonPropertyName("family")] string Family,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("tokenizer")] string? Tokenizer,
    [property: JsonPropertyName("limits")] GitHubCopilotModelLimits? Limits,
    [property: JsonPropertyName("supports")] GitHubCopilotModelSupports? Supports);

/// <summary>
/// GitHub Copilot 模型限制信息。
/// GitHub Copilot model limit information.
/// </summary>
/// <param name="MaxContextWindowTokens">
/// 最大上下文窗口 token 数。
/// Maximum context window tokens.
/// </param>
/// <param name="MaxOutputTokens">
/// 最大输出 token 数。
/// Maximum output tokens.
/// </param>
/// <param name="MaxPromptTokens">
/// 最大提示词 token 数。
/// Maximum prompt tokens.
/// </param>
/// <param name="MaxNonStreamingOutputTokens">
/// 非流式输出时的最大输出 token 数。
/// Maximum output tokens for non-streaming responses.
/// </param>
/// <param name="Vision">
/// 图像相关限制。
/// Vision-related limits.
/// </param>
public record struct GitHubCopilotModelLimits(
    [property: JsonPropertyName("max_context_window_tokens")] int? MaxContextWindowTokens,
    [property: JsonPropertyName("max_output_tokens")] int? MaxOutputTokens,
    [property: JsonPropertyName("max_prompt_tokens")] int? MaxPromptTokens,
    [property: JsonPropertyName("max_non_streaming_output_tokens")] int? MaxNonStreamingOutputTokens,
    [property: JsonPropertyName("vision")] GitHubCopilotVisionLimits? Vision);

/// <summary>
/// GitHub Copilot 视觉能力限制。
/// GitHub Copilot vision capability limits.
/// </summary>
/// <param name="MaxPromptImageSize">
/// 单张输入图片最大大小。
/// Maximum size of a single prompt image.
/// </param>
/// <param name="MaxPromptImages">
/// 最大输入图片数量。
/// Maximum number of prompt images.
/// </param>
/// <param name="SupportedMediaTypes">
/// 支持的媒体类型列表。
/// List of supported media types.
/// </param>
public record struct GitHubCopilotVisionLimits(
    [property: JsonPropertyName("max_prompt_image_size")] long? MaxPromptImageSize,
    [property: JsonPropertyName("max_prompt_images")] int? MaxPromptImages,
    [property: JsonPropertyName("supported_media_types")] List<string>? SupportedMediaTypes);

/// <summary>
/// GitHub Copilot 模型功能支持矩阵。
/// GitHub Copilot model feature support matrix.
/// </summary>
/// <param name="Streaming">
/// 是否支持流式输出。
/// Whether streaming is supported.
/// </param>
/// <param name="ToolCalls">
/// 是否支持工具调用。
/// Whether tool calling is supported.
/// </param>
/// <param name="ParallelToolCalls">
/// 是否支持并行工具调用。
/// Whether parallel tool calls are supported.
/// </param>
/// <param name="Vision">
/// 是否支持视觉输入。
/// Whether vision input is supported.
/// </param>
/// <param name="StructuredOutputs">
/// 是否支持结构化输出。
/// Whether structured outputs are supported.
/// </param>
/// <param name="AdaptiveThinking">
/// 是否支持自适应思考。
/// Whether adaptive thinking is supported.
/// </param>
/// <param name="MaxThinkingBudget">
/// 最大思考预算。
/// Maximum thinking budget.
/// </param>
/// <param name="MinThinkingBudget">
/// 最小思考预算。
/// Minimum thinking budget.
/// </param>
/// <param name="ReasoningEffort">
/// 支持的推理强度枚举。
/// Supported reasoning effort values.
/// </param>
public record struct GitHubCopilotModelSupports(
    [property: JsonPropertyName("streaming")] bool? Streaming,
    [property: JsonPropertyName("tool_calls")] bool? ToolCalls,
    [property: JsonPropertyName("parallel_tool_calls")] bool? ParallelToolCalls,
    [property: JsonPropertyName("vision")] bool? Vision,
    [property: JsonPropertyName("structured_outputs")] bool? StructuredOutputs,
    [property: JsonPropertyName("adaptive_thinking")] bool? AdaptiveThinking,
    [property: JsonPropertyName("max_thinking_budget")] int? MaxThinkingBudget,
    [property: JsonPropertyName("min_thinking_budget")] int? MinThinkingBudget,
    [property: JsonPropertyName("reasoning_effort")] List<string>? ReasoningEffort);

/// <summary>
/// GitHub Copilot 模型策略信息。
/// GitHub Copilot model policy information.
/// </summary>
/// <param name="State">
/// 策略状态。
/// Policy state.
/// </param>
/// <param name="Terms">
/// 策略条款说明。
/// Policy terms description.
/// </param>
public record struct GitHubCopilotModelPolicy(
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("terms")] string Terms);
