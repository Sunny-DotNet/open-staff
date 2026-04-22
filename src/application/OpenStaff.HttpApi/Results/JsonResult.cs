namespace OpenStaff.HttpApi.Results;

public interface IJsonResultEnvelope
{
    bool Success { get; }

    string? Message { get; }

    object? DataObject { get; }
}

public sealed record JsonResult<TData>(bool Success, TData Data, string? Message = null) : IJsonResultEnvelope
{
    object? IJsonResultEnvelope.DataObject => Data;
}

public static class JsonResultFactory
{
    public static IJsonResultEnvelope Create(bool success, object? data, string? message = null)
    {
        var dataType = data?.GetType() ?? typeof(object);
        var wrapperType = typeof(JsonResult<>).MakeGenericType(dataType);
        return (IJsonResultEnvelope)Activator.CreateInstance(wrapperType, success, data, message)!;
    }
}
