using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenStaff.HttpApi.Host.Middleware;

namespace OpenStaff.Tests.Unit;

public class ErrorHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WritesWrappedJsonEnvelope_WhenExceptionOccurs()
    {
        var middleware = new ErrorHandlingMiddleware(
            _ => throw new KeyNotFoundException("missing project"),
            NullLogger<ErrorHandlingMiddleware>.Instance,
            Microsoft.Extensions.Options.Options.Create(new JsonOptions()));

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.False(document.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("missing project", document.RootElement.GetProperty("message").GetString());
        var data = document.RootElement.GetProperty("data");
        Assert.Equal("missing project", data.GetProperty("error").GetString());
        Assert.Equal(StatusCodes.Status404NotFound, data.GetProperty("statusCode").GetInt32());
    }
}
