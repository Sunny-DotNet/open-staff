using Microsoft.OpenApi;
using OpenStaff.HttpApi.Host.OpenApi;

namespace OpenStaff.Tests.Unit;

public class JsonEnvelopeOperationTransformerTests
{
    [Fact]
    public async Task TransformAsync_WrapsJsonResponsesInEnvelope()
    {
        var transformer = new JsonEnvelopeOperationTransformer();
        var payloadSchema = new OpenApiSchema { Type = JsonSchemaType.String };
        var operation = new OpenApiOperation
        {
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse
                {
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new()
                        {
                            Schema = payloadSchema
                        }
                    }
                }
            }
        };

        await transformer.TransformAsync(operation, null!, CancellationToken.None);

        var schema = operation.Responses["200"].Content["application/json"].Schema;

        Assert.Equal(JsonSchemaType.Object, schema.Type);
        Assert.True(schema.Properties.ContainsKey("success"));
        Assert.True(schema.Properties.ContainsKey("data"));
        Assert.True(schema.Properties.ContainsKey("message"));
        Assert.Same(payloadSchema, schema.Properties["data"]);
        Assert.Contains("success", schema.Required);
        Assert.Contains("data", schema.Required);
    }

    [Fact]
    public async Task TransformAsync_SkipsNonJsonResponses()
    {
        var transformer = new JsonEnvelopeOperationTransformer();
        var payloadSchema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" };
        var operation = new OpenApiOperation
        {
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse
                {
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/octet-stream"] = new()
                        {
                            Schema = payloadSchema
                        }
                    }
                }
            }
        };

        await transformer.TransformAsync(operation, null!, CancellationToken.None);

        var schema = operation.Responses["200"].Content["application/octet-stream"].Schema;

        Assert.Same(payloadSchema, schema);
        Assert.Equal(JsonSchemaType.String, schema.Type);
        Assert.Equal("binary", schema.Format);
    }
}
