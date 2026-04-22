using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.WebUtilities;
using OpenStaff.HttpApi.Results;
using MvcJsonResult = Microsoft.AspNetCore.Mvc.JsonResult;

namespace OpenStaff.HttpApi.Filters;

public sealed class JsonResultEnvelopeFilter : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        switch (context.Result)
        {
            case NoContentResult:
            case FileResult:
                await next();
                return;

            case ObjectResult objectResult:
                WrapObjectResult(objectResult);
                break;

            case MvcJsonResult jsonResult:
                WrapJsonResult(jsonResult);
                break;

            case StatusCodeResult statusCodeResult when ShouldWrapStatusCode(statusCodeResult.StatusCode):
                context.Result = CreateWrappedStatusCodeResult(statusCodeResult.StatusCode);
                break;
        }

        await next();
    }

    private static void WrapObjectResult(ObjectResult objectResult)
    {
        if (objectResult.Value is IJsonResultEnvelope)
            return;

        var statusCode = objectResult.StatusCode ?? StatusCodes.Status200OK;
        var wrapped = JsonResultFactory.Create(
            success: IsSuccessStatusCode(statusCode),
            data: objectResult.Value,
            message: ExtractMessage(objectResult.Value, statusCode));

        objectResult.Value = wrapped;
        objectResult.DeclaredType = wrapped.GetType();
    }

    private static void WrapJsonResult(MvcJsonResult jsonResult)
    {
        if (jsonResult.Value is IJsonResultEnvelope)
            return;

        var statusCode = jsonResult.StatusCode ?? StatusCodes.Status200OK;
        jsonResult.Value = JsonResultFactory.Create(
            success: IsSuccessStatusCode(statusCode),
            data: jsonResult.Value,
            message: ExtractMessage(jsonResult.Value, statusCode));
    }

    private static ObjectResult CreateWrappedStatusCodeResult(int statusCode)
    {
        var wrapped = JsonResultFactory.Create(
            success: IsSuccessStatusCode(statusCode),
            data: null,
            message: ExtractMessage(null, statusCode));

        return new ObjectResult(wrapped)
        {
            StatusCode = statusCode,
            DeclaredType = wrapped.GetType()
        };
    }

    private static bool IsSuccessStatusCode(int statusCode) => statusCode is >= 200 and < 300;

    private static bool ShouldWrapStatusCode(int statusCode) => statusCode is not StatusCodes.Status204NoContent and not StatusCodes.Status304NotModified;

    private static string? ExtractMessage(object? value, int statusCode)
    {
        if (IsSuccessStatusCode(statusCode))
            return null;

        if (value is null)
            return ReasonPhrases.GetReasonPhrase(statusCode);

        if (value is ProblemDetails problemDetails)
            return FirstNonEmpty(problemDetails.Detail, problemDetails.Title, ReasonPhrases.GetReasonPhrase(statusCode));

        if (value is string text)
            return string.IsNullOrWhiteSpace(text) ? ReasonPhrases.GetReasonPhrase(statusCode) : text;

        if (TryGetStringProperty(value, "message", out var message)
            || TryGetStringProperty(value, "error", out message)
            || TryGetStringProperty(value, "title", out message))
        {
            return message;
        }

        return ReasonPhrases.GetReasonPhrase(statusCode);
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static bool TryGetStringProperty(object value, string propertyName, out string? result)
    {
        var property = value
            .GetType()
            .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);

        result = property?.GetValue(value) as string;
        return !string.IsNullOrWhiteSpace(result);
    }
}
