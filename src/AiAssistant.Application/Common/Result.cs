namespace AiAssistant.Application.Common;

public enum ErrorKind { Validation, Unauthorized, Forbidden, NotFound, Conflict, Limit, Unavailable }

public sealed record AppError(ErrorKind Kind, string Code, string Message, IDictionary<string, string[]>? Fields = null);

public readonly record struct Result<T>(T? Value, AppError? Error)
{
    public bool IsSuccess => Error is null;
    public static Result<T> Ok(T value) => new(value, null);
    public static Result<T> Fail(ErrorKind kind, string code, string message) => new(default, new(kind, code, message));
    public static Result<T> Invalid(IDictionary<string, string[]> fields) =>
        new(default, new(ErrorKind.Validation, "validation", "Проверьте введённые данные.", fields));
}

public sealed class DataConflictException(Exception innerException) : Exception("Database uniqueness conflict.", innerException);
