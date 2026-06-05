using Microsoft.AspNetCore.Mvc;
using Wms.Api.Infrastructure;
using Wms.Application.Common.Messaging;
using Wms.Application.Handlers.Dashboard.Queries;
using Wms.Shared.Common;

namespace Wms.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    [HttpGet("overview")]
    public async Task<IResult> GetOverview(
        [FromQuery] int days,
        [FromServices] IQueryHandler<GetDashboardOverviewQuery, DashboardOverviewDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new GetDashboardOverviewQuery(days == 0 ? 14 : days),
            cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }

    [HttpGet("inbound")]
    public async Task<IResult> GetInbound(
        [FromQuery] int days,
        [FromServices] IQueryHandler<GetInboundOverviewQuery, InboundOverviewDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new GetInboundOverviewQuery(days == 0 ? 14 : days),
            cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }

    [HttpGet("outbound")]
    public async Task<IResult> GetOutbound(
        [FromQuery] int days,
        [FromServices] IQueryHandler<GetOutboundOverviewQuery, OutboundOverviewDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new GetOutboundOverviewQuery(days == 0 ? 14 : days),
            cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }
}
