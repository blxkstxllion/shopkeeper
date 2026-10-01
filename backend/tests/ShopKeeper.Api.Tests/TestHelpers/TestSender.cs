namespace ShopKeeper.Api.Tests.TestHelpers;

using MediatR;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Application.Dashboard.Queries;
using ShopKeeper.Application.Inventory.Commands;
using ShopKeeper.Application.Reports.Commands;
using ShopKeeper.Application.Reports.Queries;
using ShopKeeper.Infrastructure.Ai;
using ShopKeeper.Infrastructure.Documents;

/// <summary>Minimal ISender for handler-calls-handler tests (e.g. RestockFromSupplierCommand
/// delegating to AdjustStockCommand) - routes to the real handler using the same IAppDbContext
/// and user as the caller, instead of standing up a full MediatR DI pipeline this test suite
/// doesn't otherwise need. summarizer/renderer default to the same real, dev-mode-equivalent
/// implementations ReportExportTests already uses directly, only needed by
/// GenerateBusinessReportCommand's handler.</summary>
public class TestSender(
    IAppDbContext db, ICurrentUserService currentUser,
    IReportSummarizer? summarizer = null, IReportDocumentRenderer? renderer = null) : ISender
{
    private readonly IReportSummarizer _summarizer = summarizer ?? new PassthroughReportSummarizer();
    private readonly IReportDocumentRenderer _renderer = renderer ?? new QuestPdfReportRenderer();

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        if (request is AdjustStockCommand adjustStock)
        {
            return (Task<TResponse>)(object)new AdjustStockCommandHandler(db, currentUser, new NotificationDispatcher(db)).Handle(adjustStock, cancellationToken);
        }
        if (request is GetDashboardSummaryQuery dashboardSummary)
        {
            return (Task<TResponse>)(object)new GetDashboardSummaryQueryHandler(db, currentUser).Handle(dashboardSummary, cancellationToken);
        }
        if (request is GetProfitabilityReportQuery profitability)
        {
            return (Task<TResponse>)(object)new GetProfitabilityReportQueryHandler(db, currentUser).Handle(profitability, cancellationToken);
        }
        if (request is GetExpenseReportQuery expenses)
        {
            return (Task<TResponse>)(object)new GetExpenseReportQueryHandler(db, currentUser).Handle(expenses, cancellationToken);
        }
        if (request is GetInventoryReportQuery inventory)
        {
            return (Task<TResponse>)(object)new GetInventoryReportQueryHandler(db, currentUser).Handle(inventory, cancellationToken);
        }
        if (request is GenerateBusinessReportCommand generateReport)
        {
            return (Task<TResponse>)(object)new GenerateBusinessReportCommandHandler(this, db, currentUser, _summarizer, _renderer)
                .Handle(generateReport, cancellationToken);
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
