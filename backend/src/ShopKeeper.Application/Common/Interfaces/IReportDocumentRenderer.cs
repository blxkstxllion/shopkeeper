namespace ShopKeeper.Application.Common.Interfaces;

using ShopKeeper.Application.Reports.Dtos;

/// <summary>Renders a formatted business report document (PDF or Word) from data the caller has
/// already fetched and verified - the renderer only lays it out, it never queries or computes
/// anything itself.</summary>
public interface IReportDocumentRenderer
{
    Task<byte[]> RenderAsync(ReportDocumentModel model, ReportExportFormat format, CancellationToken ct = default);
}

public enum ReportExportFormat
{
    Pdf,
    Word,
}

public record ReportDocumentModel(
    string BusinessName,
    string CurrencyCode,
    DateOnly From,
    DateOnly To,
    string BranchLabel,
    string SummaryText,
    ProfitabilityReportDto Profitability,
    ExpenseReportDto Expenses,
    InventoryReportDto Inventory);
