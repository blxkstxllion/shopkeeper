namespace ShopKeeper.Api.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopKeeper.Application.Common.Dtos;
using ShopKeeper.Application.Customers.Commands;
using ShopKeeper.Application.Customers.Dtos;
using ShopKeeper.Application.Customers.Queries;
using ShopKeeper.Domain.Enums;

[Authorize]
[ApiController]
[Route("api/customers")]
public class CustomersController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<CustomerDto>>> GetAll(
        [FromQuery] string? search, [FromQuery] bool activeOnly = true, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(await mediator.Send(new GetCustomersQuery(search, activeOnly, page, pageSize), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerDetailDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetCustomerDetailQuery(id), ct));

    [HttpPost]
    public async Task<ActionResult<CustomerDto>> Create(CreateCustomerCommand command, CancellationToken ct) =>
        Ok(await mediator.Send(command, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateCustomerCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest();
        await mediator.Send(command, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid? clientRequestId, CancellationToken ct)
    {
        await mediator.Send(new DeleteCustomerCommand(id, clientRequestId), ct);
        return NoContent();
    }

    public record RecordPaymentRequest(decimal Amount, PaymentMethod Method, string? ReferenceNumber, string? Note, Guid? ClientRequestId);

    [HttpPost("{id:guid}/payments")]
    public async Task<ActionResult<CustomerLedgerEntryDto>> RecordPayment(Guid id, RecordPaymentRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(
            new RecordCustomerPaymentCommand(id, request.Amount, request.Method, request.ReferenceNumber, request.Note, request.ClientRequestId),
            ct));

    [HttpGet("{id:guid}/ledger")]
    public async Task<ActionResult<PagedResult<CustomerLedgerEntryDto>>> GetLedger(
        Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await mediator.Send(new GetCustomerLedgerQuery(id, page, pageSize), ct));
}
