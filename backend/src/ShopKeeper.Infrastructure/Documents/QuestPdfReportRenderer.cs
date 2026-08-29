namespace ShopKeeper.Infrastructure.Documents;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShopKeeper.Application.Common.Interfaces;

/// <summary>
/// Renders the exported business report to PDF (via QuestPDF) or Word (via WordReportBuilder,
/// DocumentFormat.OpenXml) - both formats share the same TrendChartRenderer-drawn PNG for the
/// one chart in the document, and lay out the same sections/tables from the same
/// ReportDocumentModel, so the two formats never drift from each other in content.
/// </summary>
public class QuestPdfReportRenderer : IReportDocumentRenderer
{
    public Task<byte[]> RenderAsync(ReportDocumentModel model, ReportExportFormat format, CancellationToken ct = default)
    {
        var chartPng = TrendChartRenderer.Draw(model.Profitability.DailyTrend);
        byte[] bytes = format switch
        {
            ReportExportFormat.Pdf => RenderPdf(model, chartPng),
            ReportExportFormat.Word => WordReportBuilder.Build(model, chartPng),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown report export format."),
        };
        return Task.FromResult(bytes);
    }

    private static byte[] RenderPdf(ReportDocumentModel model, byte[] chartPng)
    {
        string Money(decimal amount) => $"{model.CurrencyCode} {amount:N2}";

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text(model.BusinessName).FontSize(18).Bold();
                    col.Item().Text($"Business report - {model.From:yyyy-MM-dd} to {model.To:yyyy-MM-dd} - {model.BranchLabel}")
                        .FontSize(10).FontColor(Colors.Grey.Darken1);
                });

                page.Content().Column(col =>
                {
                    col.Spacing(14);

                    col.Item().Text(model.SummaryText).FontSize(10.5f).LineHeight(1.3f);

                    col.Item().Row(row =>
                    {
                        StatCell(row.RelativeItem(), "Revenue", Money(model.Profitability.Totals.Revenue));
                        StatCell(row.RelativeItem(), "Gross profit", $"{Money(model.Profitability.Totals.GrossProfit)} ({model.Profitability.Totals.GrossMarginPercent:0.#}%)");
                        StatCell(row.RelativeItem(), "Net profit", $"{Money(model.Profitability.Totals.NetProfit)} ({model.Profitability.Totals.NetMarginPercent:0.#}%)");
                        StatCell(row.RelativeItem(), "Total expenses", Money(model.Expenses.TotalAmount));
                    });

                    col.Item().Text("Revenue & net profit").Bold();
                    col.Item().Image(chartPng);

                    if (model.Profitability.ByCategory.Count > 0)
                    {
                        col.Item().Text("Profit by category").Bold();
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(); });
                            table.Header(h => { HeaderCell(h, "Category"); HeaderCell(h, "Profit"); });
                            foreach (var c in model.Profitability.ByCategory)
                            {
                                DataCell(table, c.CategoryName);
                                DataCell(table, Money(c.Profit));
                            }
                        });
                    }

                    if (model.Profitability.ByBranch.Count > 0)
                    {
                        col.Item().Text("Branch comparison").Bold();
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                            table.Header(h => { HeaderCell(h, "Branch"); HeaderCell(h, "Revenue"); HeaderCell(h, "Profit"); HeaderCell(h, "Margin"); });
                            foreach (var b in model.Profitability.ByBranch)
                            {
                                DataCell(table, b.BranchName);
                                DataCell(table, Money(b.Revenue));
                                DataCell(table, Money(b.Profit));
                                DataCell(table, $"{b.MarginPercent:0.#}%");
                            }
                        });
                    }

                    if (model.Profitability.TopProducts.Count > 0 || model.Profitability.WorstProducts.Count > 0)
                    {
                        col.Item().Text("Top & worst products by profit").Bold();
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(); c.RelativeColumn(2); c.RelativeColumn(); });
                            table.Header(h => { HeaderCell(h, "Top product"); HeaderCell(h, "Profit"); HeaderCell(h, "Worst product"); HeaderCell(h, "Profit"); });
                            var rows = Math.Max(model.Profitability.TopProducts.Count, model.Profitability.WorstProducts.Count);
                            for (var i = 0; i < rows; i++)
                            {
                                var top = i < model.Profitability.TopProducts.Count ? model.Profitability.TopProducts[i] : null;
                                var worst = i < model.Profitability.WorstProducts.Count ? model.Profitability.WorstProducts[i] : null;
                                DataCell(table, top?.ProductName ?? "");
                                DataCell(table, top is null ? "" : Money(top.Profit));
                                DataCell(table, worst?.ProductName ?? "");
                                DataCell(table, worst is null ? "" : Money(worst.Profit));
                            }
                        });
                    }

                    if (model.Expenses.ByCategory.Count > 0)
                    {
                        col.Item().Text("Expenses by category").Bold();
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(); c.RelativeColumn(); });
                            table.Header(h => { HeaderCell(h, "Category"); HeaderCell(h, "Amount"); HeaderCell(h, "% of total"); });
                            foreach (var e in model.Expenses.ByCategory)
                            {
                                DataCell(table, e.CategoryName);
                                DataCell(table, Money(e.Amount));
                                DataCell(table, $"{e.PercentOfTotal:0.#}%");
                            }
                        });
                    }

                    col.Item().Text("Inventory").Bold();
                    col.Item().Row(row =>
                    {
                        StatCell(row.RelativeItem(), "Total products", model.Inventory.Valuation.TotalProducts.ToString());
                        StatCell(row.RelativeItem(), "Low stock", model.Inventory.Valuation.LowStockCount.ToString());
                        StatCell(row.RelativeItem(), "Out of stock", model.Inventory.Valuation.OutOfStockCount.ToString());
                        StatCell(row.RelativeItem(), "Inventory value", Money(model.Inventory.Valuation.InventoryValue));
                    });

                    if (model.Inventory.OutOfStockProducts.Count > 0 || model.Inventory.LowStockProducts.Count > 0)
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(); c.RelativeColumn(); });
                            table.Header(h => { HeaderCell(h, "Product"); HeaderCell(h, "On hand"); HeaderCell(h, "Reorder level"); });
                            foreach (var p in model.Inventory.OutOfStockProducts.Concat(model.Inventory.LowStockProducts))
                            {
                                DataCell(table, p.ProductName);
                                DataCell(table, p.QuantityOnHand.ToString());
                                DataCell(table, p.MinimumStock.ToString());
                            }
                        });
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private static void StatCell(QuestPDF.Infrastructure.IContainer container, string label, string value)
    {
        container.Column(col =>
        {
            col.Item().Text(label).FontSize(8).FontColor(Colors.Grey.Darken1);
            col.Item().Text(value).FontSize(12).Bold();
        });
    }

    private static void HeaderCell(TableCellDescriptor header, string text) =>
        header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten1).PaddingBottom(4)
            .Text(text).FontSize(9).Bold().FontColor(Colors.Grey.Darken2);

    private static void DataCell(TableDescriptor table, string text) =>
        table.Cell().PaddingVertical(3).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Text(text).FontSize(9.5f);
}
