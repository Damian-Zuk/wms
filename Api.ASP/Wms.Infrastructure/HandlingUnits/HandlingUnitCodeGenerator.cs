using Microsoft.EntityFrameworkCore;
using Wms.Application.HandlingUnits;
using Wms.Infrastructure.Data;

namespace Wms.Infrastructure.HandlingUnits;

/// <summary>
/// Draws codes from the HandlingUnitCodes database sequence so concurrent
/// requests can never be handed the same number.
/// </summary>
internal sealed class HandlingUnitCodeGenerator(AppDbContext context) : IHandlingUnitCodeGenerator
{
    public async Task<string> NextCodeAsync(CancellationToken cancellationToken)
    {
        var value = await context.Database
            .SqlQueryRaw<long>("SELECT nextval('\"HandlingUnitCodes\"') AS \"Value\"")
            .SingleAsync(cancellationToken);

        return $"HU-{value:D6}";
    }
}
