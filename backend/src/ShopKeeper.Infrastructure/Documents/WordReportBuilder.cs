namespace ShopKeeper.Infrastructure.Documents;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ShopKeeper.Application.Common.Interfaces;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

/// <summary>
/// Builds the Word (.docx) version of the exported business report via the OpenXml SDK - same
/// sections/tables as QuestPdfReportRenderer's PDF output, same chart PNG embedded, so the two
/// formats never drift from each other in content.
/// </summary>
public static class WordReportBuilder
{
    private const int PixelsPerEmu = 9525; // EMU per pixel at 96 DPI - the standard OOXML conversion factor.
    private const int ChartWidthPx = 500;
    private const int ChartHeightPx = 219; // matches TrendChartRenderer's 640x280 aspect ratio, scaled down for a document page.

    public static byte[] Build(ReportDocumentModel model, byte[] chartPng)
    {
        string Money(decimal amount) => $"{model.CurrencyCode} {amount:N2}";

        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body())!;

            body.AppendChild(Heading(model.BusinessName, 32));
            body.AppendChild(Paragraph(
                $"Business report - {model.From:yyyy-MM-dd} to {model.To:yyyy-MM-dd} - {model.BranchLabel}", italic: true));
            body.AppendChild(Paragraph(model.SummaryText));

            body.AppendChild(Heading("Revenue & net profit", 24));
            AppendImage(mainPart, body, chartPng, ChartWidthPx, ChartHeightPx);

            body.AppendChild(Heading("Totals", 24));
            body.AppendChild(BuildTable(
                ["Metric", "Amount"],
                [
                    ["Revenue", Money(model.Profitability.Totals.Revenue)],
                    ["Gross profit", $"{Money(model.Profitability.Totals.GrossProfit)} ({model.Profitability.Totals.GrossMarginPercent:0.#}%)"],
                    ["Total expenses", Money(model.Expenses.TotalAmount)],
                    ["Net profit", $"{Money(model.Profitability.Totals.NetProfit)} ({model.Profitability.Totals.NetMarginPercent:0.#}%)"],
                ]));

            if (model.Profitability.ByCategory.Count > 0)
            {
                body.AppendChild(Heading("Profit by category", 24));
                body.AppendChild(BuildTable(
                    ["Category", "Profit"],
                    model.Profitability.ByCategory.Select(c => new[] { c.CategoryName, Money(c.Profit) })));
            }

            if (model.Profitability.ByBranch.Count > 0)
            {
                body.AppendChild(Heading("Branch comparison", 24));
                body.AppendChild(BuildTable(
                    ["Branch", "Revenue", "Profit", "Margin"],
                    model.Profitability.ByBranch.Select(b =>
                        new[] { b.BranchName, Money(b.Revenue), Money(b.Profit), $"{b.MarginPercent:0.#}%" })));
            }

            if (model.Profitability.TopProducts.Count > 0)
            {
                body.AppendChild(Heading("Top products by profit", 24));
                body.AppendChild(BuildTable(
                    ["Product", "Profit", "Units sold"],
                    model.Profitability.TopProducts.Select(p =>
                        new[] { p.ProductName, Money(p.Profit), p.UnitsSold.ToString() })));
            }

            if (model.Profitability.WorstProducts.Count > 0)
            {
                body.AppendChild(Heading("Worst products by profit", 24));
                body.AppendChild(BuildTable(
                    ["Product", "Profit", "Units sold"],
                    model.Profitability.WorstProducts.Select(p =>
                        new[] { p.ProductName, Money(p.Profit), p.UnitsSold.ToString() })));
            }

            if (model.Expenses.ByCategory.Count > 0)
            {
                body.AppendChild(Heading("Expenses by category", 24));
                body.AppendChild(BuildTable(
                    ["Category", "Amount", "% of total"],
                    model.Expenses.ByCategory.Select(e =>
                        new[] { e.CategoryName, Money(e.Amount), $"{e.PercentOfTotal:0.#}%" })));
            }

            body.AppendChild(Heading("Inventory", 24));
            body.AppendChild(BuildTable(
                ["Metric", "Value"],
                [
                    ["Total products", model.Inventory.Valuation.TotalProducts.ToString()],
                    ["Low stock", model.Inventory.Valuation.LowStockCount.ToString()],
                    ["Out of stock", model.Inventory.Valuation.OutOfStockCount.ToString()],
                    ["Inventory value", Money(model.Inventory.Valuation.InventoryValue)],
                ]));

            var stockAlerts = model.Inventory.OutOfStockProducts.Concat(model.Inventory.LowStockProducts).ToList();
            if (stockAlerts.Count > 0)
            {
                body.AppendChild(Heading("Stock alerts", 24));
                body.AppendChild(BuildTable(
                    ["Product", "On hand", "Reorder level"],
                    stockAlerts.Select(p => new[] { p.ProductName, p.QuantityOnHand.ToString(), p.ReorderLevel.ToString() })));
            }

            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    private static Paragraph Heading(string text, int fontSize) => new(
        new ParagraphProperties(new SpacingBetweenLines { Before = "240", After = "120" }),
        new Run(new RunProperties(new Bold(), new FontSize { Val = fontSize.ToString() }), new Text(text)));

    private static Paragraph Paragraph(string text, bool italic = false)
    {
        var runProps = italic ? new RunProperties(new Italic()) : new RunProperties();
        return new Paragraph(
            new ParagraphProperties(new SpacingBetweenLines { After = "160" }),
            new Run(runProps, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }

    private static Table BuildTable(string[] headers, IEnumerable<string[]> rows)
    {
        var table = new Table();
        table.AppendChild(new TableProperties(
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4 },
                new BottomBorder { Val = BorderValues.Single, Size = 4 },
                new LeftBorder { Val = BorderValues.Single, Size = 4 },
                new RightBorder { Val = BorderValues.Single, Size = 4 },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }),
            new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" }));

        var headerRow = new TableRow();
        foreach (var h in headers)
        {
            headerRow.Append(new TableCell(new Paragraph(new Run(new RunProperties(new Bold()), new Text(h)))));
        }
        table.Append(headerRow);

        foreach (var row in rows)
        {
            var tableRow = new TableRow();
            foreach (var cell in row)
            {
                tableRow.Append(new TableCell(new Paragraph(new Run(new Text(cell)))));
            }
            table.Append(tableRow);
        }

        return table;
    }

    private static void AppendImage(MainDocumentPart mainPart, Body body, byte[] pngBytes, int widthPx, int heightPx)
    {
        var imagePart = mainPart.AddImagePart(ImagePartType.Png);
        using (var imageStream = new MemoryStream(pngBytes))
        {
            imagePart.FeedData(imageStream);
        }
        var relationshipId = mainPart.GetIdOfPart(imagePart);

        long widthEmu = (long)widthPx * PixelsPerEmu;
        long heightEmu = (long)heightPx * PixelsPerEmu;

        var picture = new PIC.Picture(
            new PIC.NonVisualPictureProperties(
                new PIC.NonVisualDrawingProperties { Id = 0, Name = "chart.png" },
                new PIC.NonVisualPictureDrawingProperties()),
            new PIC.BlipFill(
                new A.Blip { Embed = relationshipId },
                new A.Stretch(new A.FillRectangle())),
            new PIC.ShapeProperties(
                new A.Transform2D(
                    new A.Offset { X = 0, Y = 0 },
                    new A.Extents { Cx = widthEmu, Cy = heightEmu }),
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }));

        var graphicData = new A.GraphicData(picture) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" };

        var inline = new DW.Inline(
            new DW.Extent { Cx = widthEmu, Cy = heightEmu },
            new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
            new DW.DocProperties { Id = 1, Name = "Chart" },
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
            new A.Graphic(graphicData))
        {
            DistanceFromTop = 0,
            DistanceFromBottom = 0,
            DistanceFromLeft = 0,
            DistanceFromRight = 0,
        };

        body.AppendChild(new Paragraph(new Run(new Drawing(inline))));
    }
}
