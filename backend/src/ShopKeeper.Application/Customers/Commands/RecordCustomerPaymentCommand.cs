namespace ShopKeeper.Application.Customers.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Customers.Dtos;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;
using ShopKeeper.Domain.Enums;

public record RecordCustomerPaymentCommand(
    Guid CustomerId, decimal Amount, PaymentMethod Method, string? ReferenceNumber, string? Note, Guid? ClientRequestId = null)
    : IRequest<CustomerLedgerEntryDto>;

public class RecordCustomerPaymentCommandValidator : AbstractValidator<RecordCustomerPaymentCommand>
{
    public RecordCustomerPaymentCommandValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.ReferenceNumber).MaximumLength(100);
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

/// <summary>
/// A customer repaying some or all of what they owe. Deliberately NOT capped at the current
/// balance - overpayment just drives CurrentBalance negative (a credit in the customer's
/// favor, applied against their next purchase), which is simpler and more honest than
/// rejecting a cashier who's holding cash the customer actually handed over.
/// </summary>
public class RecordCustomerPaymentCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<RecordCustomerPaymentCommand, CustomerLedgerEntryDto>
{
    public async Task<CustomerLedgerEntryDto> Handle(RecordCustomerPaymentCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.CustomersManage);
        var businessId = currentUser.RequireBusinessId();
        var userId = currentUser.RequireUserId();

        // Idempotent replay: same precheck-then-catch-the-race shape as Sale/Refund's own
        // ClientRequestId mechanisms - a repayment moves real money, a retried submission must
        // never apply twice.
        if (request.ClientRequestId.HasValue)
        {
            var existing = await FindByClientRequestIdAsync(businessId, request.ClientRequestId.Value, cancellationToken);
            if (existing is not null)
            {
                return existing;
            }
        }

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.CustomerId);

        customer.CurrentBalance -= request.Amount;
        customer.RowVersion++;

        var entry = new CustomerLedgerEntry
        {
            BusinessId = businessId,
            CustomerId = customer.Id,
            Type = CustomerLedgerEntryType.Payment,
            Amount = -request.Amount,
            BalanceAfter = customer.CurrentBalance,
            ReferenceType = "CustomerPayment",
            Method = request.Method,
            ReferenceNumber = request.ReferenceNumber,
            Note = request.Note,
            CreatedByUserId = userId,
            ClientRequestId = request.ClientRequestId,
        };
        // Self-referential - a repayment has no separate parent document, this entry is the
        // full record. ReferenceId is set after Add so entry.Id (assigned by BaseEntity) is
        // already populated.
        entry.ReferenceId = entry.Id;

        db.CustomerLedgerEntries.Add(entry);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A concurrent charge/payment/refund moved this customer's balance between our
            // read and this write - Customer.RowVersion is what catches it. Same shape as
            // CreateSaleCommand/RefundSaleCommand's identical handling.
            throw new ConflictException("This customer's balance changed while recording this payment. Please try again.");
        }
        catch (DbUpdateException) when (request.ClientRequestId.HasValue)
        {
            // Two concurrent replays of the same ClientRequestId both passed the precheck above
            // and raced to insert - the partial unique index on (BusinessId, ClientRequestId)
            // caught it. Same shape as Sale/Refund's identical race handling.
            var winner = await FindByClientRequestIdAsync(businessId, request.ClientRequestId.Value, cancellationToken);
            if (winner is null)
            {
                throw;
            }

            return winner;
        }

        return ToDto(entry);
    }

    private async Task<CustomerLedgerEntryDto?> FindByClientRequestIdAsync(Guid businessId, Guid clientRequestId, CancellationToken ct)
    {
        var existing = await db.CustomerLedgerEntries
            .FirstOrDefaultAsync(e => e.BusinessId == businessId && e.ClientRequestId == clientRequestId, ct);
        return existing is null ? null : ToDto(existing);
    }

    private static CustomerLedgerEntryDto ToDto(CustomerLedgerEntry entry) => new(
        entry.Id, entry.Type.ToString(), entry.Amount, entry.BalanceAfter, entry.ReferenceType, entry.ReferenceId,
        entry.Method?.ToString(), entry.ReferenceNumber, entry.Note, entry.CreatedAt);
}
