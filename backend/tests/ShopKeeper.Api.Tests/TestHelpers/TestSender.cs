namespace ShopKeeper.Api.Tests.TestHelpers;

using MediatR;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Inventory.Commands;

/// <summary>Minimal ISender for handler-calls-handler tests (e.g. RestockFromSupplierCommand
/// delegating to AdjustStockCommand) - routes to the real handler using the same IAppDbContext
/// and user as the caller, instead of standing up a full MediatR DI pipeline this test suite
/// doesn't otherwise need.</summary>
public class TestSender(IAppDbContext db, ICurrentUserService currentUser) : ISender
{
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        if (request is AdjustStockCommand adjustStock)
        {
            return (Task<TResponse>)(object)new AdjustStockCommandHandler(db, currentUser).Handle(adjustStock, cancellationToken);
        }

        throw new NotSupportedException($"TestSender does not support {request.GetType().Name}");
    }

    public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest =>
        throw new NotSupportedException();

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
