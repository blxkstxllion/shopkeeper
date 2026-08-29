namespace ShopKeeper.Api.Tests.Reports;

using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Application.Expenses.Commands;
using ShopKeeper.Application.Products.Commands;
using ShopKeeper.Application.Reports.Commands;
using ShopKeeper.Application.Sales.Commands;
using ShopKeeper.Domain.Enums;
using ShopKeeper.Infrastructure.Ai;
using ShopKeeper.Infrastructure.Documents;
using ShopKeeper.Infrastructure.Identity;

public class ReportExportTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));

    private class RecordingReportDocumentRenderer : IReportDocumentRenderer
    {
        public ReportDocumentModel? LastModel { get; private set; }

        public Task<byte[]> RenderAsync(ReportDocumentModel model, ReportExportFormat format, CancellationToken ct = default)
        {
            LastModel = model;
            return Task.FromResult(new byte[] { 1, 2, 3 });
        }
    }

    private class ThrowingReportSummarizer : IReportSummarizer
    {
        public Task<string> SummarizeAsync(ReportFacts facts, CancellationToken ct = default) =>
            throw new InvalidOperationException("Simulated Anthropic API outage.");
    }

    private async Task<(PosTestFixture.SeededBusiness seeded, TestCurrentUserService owner, ShopKeeper.Infrastructure.Persistence.AppDbContext context)> SeedWithSaleAndExpenseAsync()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var product = await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Widget", "SKU-EXPORT", null, null, null, null, 10m, 6m, 10, true, 50, seeded.BranchId),
            CancellationToken.None);
        await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(product.Id, 5, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 50m, null)]),
            CancellationToken.None);

        var expenseCategory = await new CreateExpenseCategoryCommandHandler(context, owner).Handle(
            new CreateExpenseCategoryCommand("Rent", null), CancellationToken.None);
        await new CreateExpenseCommandHandler(context, owner).Handle(
            new CreateExpenseCommand(seeded.BranchId, expenseCategory.Id, 15m, DateOnly.FromDateTime(DateTime.UtcNow), null), CancellationToken.None);

        return (seeded, owner, context);
    }

    private static (DateOnly from, DateOnly to) ThisWeekRange() =>
        (DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));

    [Fact]
    public async Task Export_BuildsModelFromRealData_WithPassthroughSummary()
    {
        var (seeded, owner, context) = await SeedWithSaleAndExpenseAsync();
        var (from, to) = ThisWeekRange();
        var renderer = new RecordingReportDocumentRenderer();

        var handler = new GenerateBusinessReportCommandHandler(
            new TestSender(context, owner), context, owner, new PassthroughReportSummarizer(), renderer);
        await handler.Handle(new GenerateBusinessReportCommand(from, to, null, ReportExportFormat.Pdf), CancellationToken.None);

        Assert.NotNull(renderer.LastModel);
        Assert.Equal(50m, renderer.LastModel!.Profitability.Totals.Revenue);
        Assert.Equal(15m, renderer.LastModel.Expenses.TotalAmount);
        Assert.Contains("GHS 50.00", renderer.LastModel.SummaryText);
        Assert.Equal("All branches", renderer.LastModel.BranchLabel);
    }

    [Fact]
    public async Task Export_WhenSummarizerThrows_FallsBackToPlainFactsSentence()
    {
        var (seeded, owner, context) = await SeedWithSaleAndExpenseAsync();
        var (from, to) = ThisWeekRange();
        var renderer = new RecordingReportDocumentRenderer();

        var handler = new GenerateBusinessReportCommandHandler(
            new TestSender(context, owner), context, owner, new ThrowingReportSummarizer(), renderer);
        await handler.Handle(new GenerateBusinessReportCommand(from, to, null, ReportExportFormat.Pdf), CancellationToken.None);

        Assert.NotNull(renderer.LastModel);
        Assert.Contains("GHS 50.00", renderer.LastModel!.SummaryText); // fallback still uses the real, correct number
    }

    [Fact]
    public async Task Export_Pdf_ProducesRealPdfBytes()
    {
        var (seeded, owner, context) = await SeedWithSaleAndExpenseAsync();
        var (from, to) = ThisWeekRange();

        var handler = new GenerateBusinessReportCommandHandler(
            new TestSender(context, owner), context, owner, new PassthroughReportSummarizer(), new QuestPdfReportRenderer());
        var result = await handler.Handle(new GenerateBusinessReportCommand(from, to, null, ReportExportFormat.Pdf), CancellationToken.None);

        Assert.True(result.Content.Length > 1000);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.EndsWith(".pdf", result.FileName);
        Assert.Equal("%PDF"u8.ToArray(), result.Content[..4]);
    }

    [Fact]
    public async Task Export_Word_ProducesRealDocxBytes()
    {
        var (seeded, owner, context) = await SeedWithSaleAndExpenseAsync();
        var (from, to) = ThisWeekRange();

        var handler = new GenerateBusinessReportCommandHandler(
            new TestSender(context, owner), context, owner, new PassthroughReportSummarizer(), new QuestPdfReportRenderer());
        var result = await handler.Handle(new GenerateBusinessReportCommand(from, to, null, ReportExportFormat.Word), CancellationToken.None);

        Assert.True(result.Content.Length > 1000);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", result.ContentType);
        Assert.EndsWith(".docx", result.FileName);
        Assert.Equal((byte)'P', result.Content[0]); // .docx is a zip archive (PK signature)
        Assert.Equal((byte)'K', result.Content[1]);
    }

    public void Dispose() => _db.Dispose();
}
