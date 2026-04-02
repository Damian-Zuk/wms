using System;
using System.Collections.Generic;
using System.Text;
using Wms.Shared.Common;

namespace Wms.Application.Errors;

public static class ProductErrors 
{
    public static readonly Error SkuExists = new("Product.SkuExists", "Product with this SKU already exists.");
}
