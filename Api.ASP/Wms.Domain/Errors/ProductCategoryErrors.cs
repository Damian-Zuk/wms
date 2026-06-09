using Wms.Shared.Common;

namespace Wms.Domain.Errors;

public static class ProductCategoryErrors
{
    public static Error NotFound(Guid id) => Error.Problem(
        "ProductCategory.NotFound",
        $"Product category with ID '{id}' not found");

    public static Error ParentNotFound(Guid parentId) => Error.Problem(
        "ProductCategory.ParentNotFound",
        $"Parent product category with ID '{parentId}' not found");

    public static Error CircularReference => Error.Problem(
        "ProductCategory.CircularReference",
        "A category cannot be moved under itself or one of its own descendants.");
}
