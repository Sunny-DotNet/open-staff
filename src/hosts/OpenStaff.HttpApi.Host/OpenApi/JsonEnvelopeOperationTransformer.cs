using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace OpenStaff.HttpApi.Host.OpenApi;

/// <summary>
/// 将 OpenAPI 中的 JSON 响应架构包装为统一的 JsonResult envelope。
/// Wraps JSON response schemas in the shared JsonResult envelope for OpenAPI generation.
/// </summary>
public sealed class JsonEnvelopeOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        foreach (var response in operation.Responses.Values)
        {
            if (response.Content is null || response.Content.Count == 0)
            {
                continue;
            }

            foreach (var content in response.Content)
            {
                if (!IsJsonContentType(content.Key) || content.Value.Schema is null)
                {
                    continue;
                }

                if (IsEnvelopeSchema(content.Value.Schema))
                {
                    continue;
                }

                content.Value.Schema = WrapSchema(content.Value.Schema);
            }
        }

        return Task.CompletedTask;
    }

    private static bool IsJsonContentType(string contentType)
        => contentType.Contains("json", StringComparison.OrdinalIgnoreCase);

    private static bool IsEnvelopeSchema(IOpenApiSchema schema)
    {
        var properties = schema.Properties;
        if (properties is null || properties.Count == 0)
        {
            return false;
        }

        return properties.ContainsKey("success")
               && properties.ContainsKey("data")
               && properties.ContainsKey("message");
    }

    private static OpenApiSchema WrapSchema(IOpenApiSchema dataSchema)
        => new()
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["success"] = new OpenApiSchema { Type = JsonSchemaType.Boolean },
                ["data"] = dataSchema,
                ["message"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.String
                }
            },
            Required = new HashSet<string>(StringComparer.Ordinal)
            {
                "success",
                "data"
            }
        };
}
