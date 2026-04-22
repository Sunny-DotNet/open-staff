
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OpenStaff.HttpApi.Results;

namespace OpenStaff.HttpApi.Host.Middleware;

/// <summary>
/// 全局错误处理中间件。
/// Global error handling middleware.
/// </summary>
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    /// <summary>
    /// 初始化全局错误处理中间件。
    /// Initializes the global error handling middleware.
    /// </summary>
    public ErrorHandlingMiddleware(
        RequestDelegate next,
        ILogger<ErrorHandlingMiddleware> logger,
        IOptions<JsonOptions> jsonOptions)
    {
        _next = next;
        _logger = logger;
        _jsonSerializerOptions = jsonOptions.Value.JsonSerializerOptions;
    }

    /// <summary>
    /// 执行中间件并捕获未处理异常。
    /// Executes the middleware and captures unhandled exceptions.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "请求处理异常 / Request processing error: {Path}", context.Request.Path);
            await HandleExceptionAsync(context, ex);
        }
    }

    /// <summary>
    /// 将异常映射为 HTTP 状态码并写入统一 JSON 错误响应。
    /// Maps the exception to an HTTP status code and writes a unified JSON error response.
    /// </summary>
    /// <param name="context">当前 HTTP 上下文，包含将被写回的响应对象。 / Current HTTP context whose response will be written.</param>
    /// <param name="exception">已捕获的异常；根据异常类型选择响应状态码。 / Captured exception whose type determines the response status code.</param>
    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = exception switch
        {
            ArgumentException => (int)HttpStatusCode.BadRequest,
            InvalidOperationException => (int)HttpStatusCode.BadRequest,
            KeyNotFoundException => (int)HttpStatusCode.NotFound,
            _ => (int)HttpStatusCode.InternalServerError
        };

        var payload = JsonResultFactory.Create(
            success: false,
            data: new
            {
                error = exception.Message,
                statusCode = context.Response.StatusCode
            },
            message: exception.Message);

        var result = JsonSerializer.Serialize(payload, payload.GetType(), _jsonSerializerOptions);

        await context.Response.WriteAsync(result);
    }
}
