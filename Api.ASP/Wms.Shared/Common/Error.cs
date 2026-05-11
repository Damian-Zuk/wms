using System;
using System.Collections.Generic;
using System.Text;

namespace Wms.Shared.Common;

public record Error
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);
    public static readonly Error NullValue = new(
        "General.Null",
        "Null value was provided",
        ErrorType.Failure);

    public Error(string code, string description, ErrorType type, string? stackTrace = null)
    {
        Code = code;
        Description = description;
        Type = type;
        StackTrace = stackTrace;
    }

    public string Code { get; }

    public string Description { get; }

    public ErrorType Type { get; }
    
    public string? StackTrace { get; }

    public static implicit operator Result(Error error) => Result.Failure(error);

    public static Error Failure(string code, string description) =>
        new(code, description, ErrorType.Failure);

    public static Error NotFound(string code, string description) =>
        new(code, description, ErrorType.NotFound);

    public static Error Problem(string code, string description) =>
        new(code, description, ErrorType.Problem);

    public static Error Conflict(string code, string description) =>
        new(code, description, ErrorType.Conflict);

    public static Error Unexpected(string code, string description) =>
        new(code, description, ErrorType.Unexpected, Environment.StackTrace);
}