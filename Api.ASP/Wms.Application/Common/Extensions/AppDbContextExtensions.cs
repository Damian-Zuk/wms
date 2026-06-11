using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Common.Extensions;

public static class AppDbContextExtensions
{
    public static async Task<Result> SaveChangesWithConcurrencyCheckAsync(
        this IAppDbContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return InventoryErrors.ConcurrencyConflict();
        }
    }
}
