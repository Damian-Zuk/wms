using Wms.Shared.Common;

namespace Wms.Domain.Errors;

public static class ProductErrors
{
    public static Error SkuExists(string Sku) => Error.Problem(
        "Product.SkuExists",
        $"Product with SKU '{Sku}' already exists.");

    public static Error NotFound(Guid productId) => Error.Problem(
        "Product.NotFound",
        $"Product with ID '{productId}' not found");
}
