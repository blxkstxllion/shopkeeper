namespace ShopKeeper.Application.Reports.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Behaviors;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Reports.Dtos;
using ShopKeeper.Application.Reports.Queries;
using ShopKeeper.Domain.Constants;

public record GenerateBusinessReportCommand(DateOnly From, DateOnly To, Guid? BranchId, ReportExportFormat Format)
    : IRequest<ExportedReportDto>, IRequirePlanFeature
{
    public bool RequiresReports => true;
    public bool RequiresAi => false;
    public bool RequiresCustomRoles => false;
}

public class GenerateBusinessReportCommandValidator : AbstractValidator<GenerateBusinessReportCommand>
{
    public GenerateBusinessReportCommandValidator()
    {
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("'To' must not be before 'From'.");
    }
}

/// <summary>
/// Bundles the same 3 report queries the Reports page already uses into one downloadable
/// document (PDF or Word), with a written executive summary on top - AI-written when Claude is
/// configured, a deterministic template otherwise (see IReportSummarizer), so the export always
/// succeeds either way. The renderer (IReportDocumentRenderer) only lays out data this handler
/// already fetched and verified - same "can't disagree with what the app shows" guarantee as
/// GetAdvisorAnswerQueryHandler.
/// </summary>
public class GenerateBusinessReportCommandHandler(
    ISender mediator, IAppDbContext db, ICurrentUserService currentUser,
    IReportSummarizer summarizer, IReportDocumentRenderer renderer)
    : IRequestHandler<GenerateBusinessReportCommand, ExportedReportDto>
{
    public async Task<ExportedReportDto> Handle(GenerateBusinessReportCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.ReportsView);

        var businessId = currentUser.RequireBusinessId();
        var business = await db.Businesses.Where(b => b.Id == businessId)
            .Select(b => new { b.Name, b.CurrencyCode }).FirstAsync(cancellationToken);

        var branchLabel = request.BranchId.HasValue
            ? await db.Branches.Where(b => b.Id == request.BranchId).Select(b => b.Name).FirstOrDefaultAsync(cancellationToken) ?? "Unknown branch"
            : "All branches";

        var profitability = await mediator.Send(new GetProfitabilityReportQuery(request.From, request.To, request.BranchId), cancellationToken);
        var expenses = await mediator.Send(new GetExpenseReportQuery(request.From, request.To, request.BranchId, null), cancellationToken);
        var inventory = await mediator.Send(new GetInventoryReportQuery(request.From, request.To, request.BranchId), cancellationToken);

        var facts = BuildFacts(business.CurrencyCode, profitability, expenses, inventory);

        string summaryText;
        try
        {
            summaryText = await summarizer.SummarizeAsync(facts, cancellationToken);
        }
        catch (Exception)
        {
            // Best-effort enhancement only - fall back to a plain facts-only sentence, so
            // export never breaks because Claude did. Mirrors GetAdvisorAnswerQueryHandler.
            summaryText = BuildFallbackSummary(facts);
        }

        var model = new ReportDocumentModel(
            business.Name, business.CurrencyCode, request.From, request.To, branchLabel,
            summaryText, profitability, expenses, inventory);

        var content = await renderer.RenderAsync(model, request.Format, cancellationToken);

        var (extension, contentType) = request.Format switch
        {
            ReportExportFormat.Pdf => ("pdf", "application/pdf"),
            ReportExportFormat.Word => ("docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
            _ => throw new ArgumentOutOfRangeException(nameof(request), $"Unknown format '{request.Format}'."),
        };
        var fileName = $"business-report-{request.From:yyyy-MM-dd}-to-{request.To:yyyy-MM-dd}.{extension}";

        return new ExportedReportDto(content, fileName, contentType);
    }

    private static ReportFacts BuildFacts(
        string currencyCode, ProfitabilityReportDto profitability, ExpenseReportDto expenses, InventoryReportDto inventory)
    {
        var topExpense = expenses.ByCategory.OrderByDescending(c => c.Amount).FirstOrDefault();
        var topProduct = profitability.TopProducts.FirstOrDefault();
        var worstProduct = profitability.WorstProducts.FirstOrDefault();

        return new ReportFacts(
            currencyCode,
            profitability.Totals.Revenue,
            profitability.Totals.Cogs,
            profitability.Totals.GrossProfit,
            profitability.Totals.GrossMarginPercent,
            profitability.Totals.TotalExpenses,
            profitability.Totals.NetProfit,
            profitability.Totals.NetMarginPercent,
            topExpense?.CategoryName,
            topExpense?.Amount,
            topProduct?.ProductName,
            topProduct?.Profit,
            worstProduct?.ProductName,
            worstProduct?.Profit,
            inventory.Valuation.LowStockCount,
            inventory.Valuation.OutOfStockCount);
    }

    private static string BuildFallbackSummary(ReportFacts f) =>
        $"Revenue was {f.CurrencyCode} {f.Revenue:N2}, net profit was {f.CurrencyCode} {f.NetProfit:N2} " +
        $"({f.NetMarginPercent:0.#}% margin), and total expenses were {f.CurrencyCode} {f.TotalExpenses:N2}. " +
        "See the tables below for the full breakdown.";
}
