using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using OpenStaff.HttpApi.Filters;
using OpenStaff.HttpApi.Results;

namespace OpenStaff.Tests.Unit;

public class JsonResultEnvelopeFilterTests
{
    [Fact]
    public async Task OnResultExecutionAsync_WrapsObjectResultPayload()
    {
        var filter = new JsonResultEnvelopeFilter();
        var payload = new { value = 42 };
        var context = CreateContext(new OkObjectResult(payload));

        await filter.OnResultExecutionAsync(context, CreateNext(context));

        var ok = Assert.IsType<OkObjectResult>(context.Result);
        var envelope = Assert.IsAssignableFrom<IJsonResultEnvelope>(ok.Value);
        Assert.True(envelope.Success);
        Assert.Null(envelope.Message);
        Assert.Same(payload, envelope.DataObject);
    }

    [Fact]
    public async Task OnResultExecutionAsync_WrapsPlainNotFoundResult()
    {
        var filter = new JsonResultEnvelopeFilter();
        var context = CreateContext(new NotFoundResult());

        await filter.OnResultExecutionAsync(context, CreateNext(context));

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        var envelope = Assert.IsAssignableFrom<IJsonResultEnvelope>(result.Value);
        Assert.False(envelope.Success);
        Assert.Equal("Not Found", envelope.Message);
        Assert.Null(envelope.DataObject);
    }

    [Fact]
    public async Task OnResultExecutionAsync_SkipsNoContentResult()
    {
        var filter = new JsonResultEnvelopeFilter();
        var context = CreateContext(new NoContentResult());

        await filter.OnResultExecutionAsync(context, CreateNext(context));

        Assert.IsType<NoContentResult>(context.Result);
    }

    [Fact]
    public async Task OnResultExecutionAsync_SkipsFileResult()
    {
        var filter = new JsonResultEnvelopeFilter();
        var context = CreateContext(new FileContentResult([1, 2, 3], "application/octet-stream"));

        await filter.OnResultExecutionAsync(context, CreateNext(context));

        Assert.IsType<FileContentResult>(context.Result);
    }

    [Fact]
    public async Task OnResultExecutionAsync_DoesNotDoubleWrapEnvelope()
    {
        var filter = new JsonResultEnvelopeFilter();
        var existing = new OpenStaff.HttpApi.Results.JsonResult<string>(true, "ok");
        var context = CreateContext(new OkObjectResult(existing));

        await filter.OnResultExecutionAsync(context, CreateNext(context));

        var ok = Assert.IsType<OkObjectResult>(context.Result);
        Assert.Same(existing, ok.Value);
    }

    private static ResultExecutingContext CreateContext(IActionResult result)
    {
        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            new ActionDescriptor());

        return new ResultExecutingContext(
            actionContext,
            [],
            result,
            controller: new object());
    }

    private static ResultExecutionDelegate CreateNext(ResultExecutingContext context)
        => () => Task.FromResult(new ResultExecutedContext(
            new ActionContext(context.HttpContext, context.RouteData, context.ActionDescriptor),
            [],
            context.Result,
            context.Controller));
}
