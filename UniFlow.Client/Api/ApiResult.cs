using System.Diagnostics.CodeAnalysis;

namespace UniFlow.Client.Api;

// Either a value from the server or an error - never an exception the page has to catch
public sealed class ApiResult<T>
{
    private ApiResult(T? value, ApiError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }
    public ApiError? Error { get; }

    // Tells the compiler: after "if (result.IsSuccess)" Value is not null, otherwise Error is not null
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static ApiResult<T> Success(T value) => new(value, null);

    public static ApiResult<T> Failure(ApiError error) => new(default, error);
}