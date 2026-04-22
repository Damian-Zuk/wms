using System.Diagnostics.CodeAnalysis;

namespace Wms.Shared.Common;

public class Result
{
    public Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None || !isSuccess && error == Error.None)
        {
            throw new ArgumentException("Invalid error", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) =>
        new(value, true, Error.None);
    public static Result<TValue> Failure<TValue>(Error error) =>
        new(default, false, error);

    public static Result ValidationFailure(ValidationError error) => error;
    public static Result<TValue> ValidationFailure<TValue>(ValidationError<TValue> error) => error;
}

public class Result<TValue> : Result
{
    private readonly TValue? _value;

    public Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    [NotNull]
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access the value of a failed result");

    public static implicit operator Result<TValue>(TValue? value) =>
        value is not null ? Success(value) : Failure<TValue>(Error.NullValue);

    public static implicit operator Result<TValue>(Error error) =>
        Failure<TValue>(error);

    public static Result<TValue> ValidationFailure(Error error) =>
        new(default, false, error);
}

public class ValidationError : Result
{
    public ValidationError(Error[] errors)
        : base(false,
            new("Validation.Failed",
                string.Join(", ", errors.Select(e => e.Description).ToList())))
    {
        Errors = errors;
    }

    public Error[] Errors { get; }

    public static implicit operator ValidationError(Error[] errors) => new(errors);
}

public sealed class ValidationError<TValue> : Result<TValue>
{
    public ValidationError(Error[] errors)
        : base(default, false, 
            new("Validation.Failed",
                string.Join(", ", errors.Select(e => e.Description).ToList())))
    {
        Errors = errors;
    }

    public Error[] Errors { get; }

    public static implicit operator ValidationError<TValue>(ValidationError error) =>
        new(error.Errors);
}

public sealed record Error(
    string Code,
    string Description,
    string? StackTrace = null)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    public static readonly Error NullValue = new("Error.NullValue", "Null value was provided");
    public static readonly Error NotFound = new("Error.NotFound", "Object not found");

    public static implicit operator Result(Error error) => Result.Failure(error);

    public static Error Problem(string code, string description) =>
        new(code, description);

    public static Error Unexpected(string code, string description) => 
        new(code, description, Environment.StackTrace);
}
