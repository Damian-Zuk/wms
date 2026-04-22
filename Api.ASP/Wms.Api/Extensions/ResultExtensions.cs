using Wms.Shared.Common;

namespace Wms.Api.Extensions;

public static class ResultExtensions
{
    public static IResult ToProblemDetails<T>(this Result<T> result)
    {
        if (result.IsSuccess)
            return Results.Ok(result.Value);

        return Results.Problem(
            type: GetProblemType(result.Error),
            title: result.Error.Code,
            detail: result.Error.Description,
            statusCode: GetStatusCode(result.Error));
    }

    public static IResult ToProblemDetails(this Result result)
    {
        if (result.IsSuccess)
            return Results.Ok();

        return Results.Problem(
            type: GetProblemType(result.Error),
            title: result.Error.Code,
            detail: result.Error.Description,
            statusCode: GetStatusCode(result.Error));
    }

    private static int GetStatusCode(Error error) => error.Code switch
    {
        "Validation.Failed" => StatusCodes.Status422UnprocessableEntity,
        "Error.NotFound" => StatusCodes.Status404NotFound,
        "Error.NullValue" => StatusCodes.Status404NotFound,
        _ => StatusCodes.Status400BadRequest
    };

    private static string GetProblemType(Error error) => error.Code switch
    {
        "Validation.Failed" => "Unprocessable Entity",
        "Error.NotFound" => "Not Found",
        "Error.NullValue" => "Not Found",
        _ => "Bad Request"
    };
}
