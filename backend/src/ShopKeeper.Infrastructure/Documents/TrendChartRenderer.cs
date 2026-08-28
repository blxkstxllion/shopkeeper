namespace ShopKeeper.Infrastructure.Documents;

using SkiaSharp;
using ShopKeeper.Application.Reports.Dtos;

/// <summary>
/// Draws the one chart embedded in exported reports (revenue vs. net profit over the daily
/// trend) as a flat PNG, shared by both the PDF and Word renderers - category/branch/product
/// breakdowns render as tables in the document instead of charts, matching how e.g. branch
/// comparison already renders as a table on the Reports page, not a chart.
/// </summary>
public static class TrendChartRenderer
{
    private const int Width = 640;
    private const int Height = 280;
    private const int PaddingLeft = 60;
    private const int PaddingRight = 20;
    private const int PaddingTop = 20;
    private const int PaddingBottom = 40;

    private static readonly SKColor RevenueColor = new(0x2a, 0x78, 0xd6);
    private static readonly SKColor ProfitColor = new(0x1b, 0xaf, 0x7a);
    private static readonly SKColor GridColor = new(0xe1, 0xe0, 0xd9);
    private static readonly SKColor AxisColor = new(0x89, 0x87, 0x81);

    public static byte[] Draw(IReadOnlyList<DailyProfitPointDto> dailyTrend)
    {
        using var bitmap = new SKBitmap(Width, Height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        if (dailyTrend.Count < 2)
        {
            return Encode(bitmap);
        }

        var plotWidth = Width - PaddingLeft - PaddingRight;
        var plotHeight = Height - PaddingTop - PaddingBottom;
        var maxValue = Math.Max(1m, dailyTrend.Max(p => Math.Max(p.Revenue, p.NetProfit)));
        var minValue = Math.Min(0m, dailyTrend.Min(p => Math.Min(p.Revenue, p.NetProfit)));
        var valueRange = Math.Max(1m, maxValue - minValue);

        float XFor(int index) => PaddingLeft + plotWidth * index / (float)(dailyTrend.Count - 1);
        float YFor(decimal value) => PaddingTop + plotHeight - (float)((value - minValue) / valueRange) * plotHeight;

        using var gridPaint = new SKPaint { Color = GridColor, StrokeWidth = 1, IsAntialias = true };
        using var axisPaint = new SKPaint { Color = AxisColor, StrokeWidth = 1, IsAntialias = true };
        for (var i = 0; i <= 4; i++)
        {
            var y = PaddingTop + plotHeight * i / 4f;
            canvas.DrawLine(PaddingLeft, y, Width - PaddingRight, y, gridPaint);
        }
        canvas.DrawLine(PaddingLeft, PaddingTop, PaddingLeft, PaddingTop + plotHeight, axisPaint);
        canvas.DrawLine(PaddingLeft, PaddingTop + plotHeight, Width - PaddingRight, PaddingTop + plotHeight, axisPaint);

        DrawSeries(canvas, dailyTrend, p => p.Revenue, RevenueColor, XFor, YFor);
        DrawSeries(canvas, dailyTrend, p => p.NetProfit, ProfitColor, XFor, YFor);

        using var legendFont = new SKFont(SKTypeface.Default, 11);
        using var legendPaint = new SKPaint { Color = AxisColor, IsAntialias = true };
        DrawLegendSwatch(canvas, PaddingLeft, Height - 14, RevenueColor);
        canvas.DrawText("Revenue", PaddingLeft + 16, Height - 10, SKTextAlign.Left, legendFont, legendPaint);
        DrawLegendSwatch(canvas, PaddingLeft + 90, Height - 14, ProfitColor);
        canvas.DrawText("Net profit", PaddingLeft + 106, Height - 10, SKTextAlign.Left, legendFont, legendPaint);

        return Encode(bitmap);
    }

    private static void DrawSeries(
        SKCanvas canvas,
        IReadOnlyList<DailyProfitPointDto> trend,
        Func<DailyProfitPointDto, decimal> selector,
        SKColor color,
        Func<int, float> xFor,
        Func<decimal, float> yFor)
    {
        var builder = new SKPathBuilder();
        for (var i = 0; i < trend.Count; i++)
        {
            var point = new SKPoint(xFor(i), yFor(selector(trend[i])));
            if (i == 0) builder.MoveTo(point);
            else builder.LineTo(point);
        }
        using var path = builder.Detach();
        using var linePaint = new SKPaint { Color = color, StrokeWidth = 2, IsAntialias = true, Style = SKPaintStyle.Stroke };
        canvas.DrawPath(path, linePaint);
    }

    private static void DrawLegendSwatch(SKCanvas canvas, float x, float y, SKColor color)
    {
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        canvas.DrawRect(x, y - 8, 10, 10, paint);
    }

    private static byte[] Encode(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
