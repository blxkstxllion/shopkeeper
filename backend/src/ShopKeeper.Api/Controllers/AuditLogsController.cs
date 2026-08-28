namespace ShopKeeper.Api.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopKeeper.Application.AuditLogs.Dtos;
using ShopKeeper.Application.AuditLogs.Queries;
using ShopKeeper.Application.Common.Dtos;

[Authorize]
[ApiController]
[Route("api/audit-logs")]
public class AuditLogsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> GetAll(
        [FromQuery] string? entityType,
        [FromQuery] string? action,
        [FromQuery] Guid? userId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        Ok(await mediator.Send(new GetAuditLogsQuery(entityType, action, userId, from, to, page, pageSize), ct));
}
