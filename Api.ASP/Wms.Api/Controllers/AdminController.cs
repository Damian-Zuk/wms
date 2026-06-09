using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wms.Infrastructure.Data;

namespace Wms.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public AdminController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // POST /api/admin/seed
    [HttpPost("seed")]
    public async Task<IActionResult> Seed(CancellationToken cancellationToken)
    {
        await WarehouseSeeder.SeedAsync(_dbContext, cancellationToken);
        return NoContent();
    }

    // POST /api/admin/truncate
    [HttpPost("truncate")]
    public async Task<IActionResult> Truncate(CancellationToken cancellationToken)
    {
        await WarehouseCleaner.ClearAsync(_dbContext, cancellationToken);
        return NoContent();
    }
}
