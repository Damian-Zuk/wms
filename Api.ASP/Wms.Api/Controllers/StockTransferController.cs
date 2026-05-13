using Microsoft.AspNetCore.Mvc;
using Wms.Api.Infrastructure;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Features.StockTransfers.Commands;
using Wms.Shared.Common;

namespace Wms.Api.Controllers;

[ApiController]
[Route("api/stock-transfers")]
public class StockTransferController : ControllerBase
{
    [HttpPost]
    public async Task<IResult> TransferStock(
        [FromBody] TransferStockCommand request,
        [FromServices] ICommandHandler<TransferStockCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(request, cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }
}
