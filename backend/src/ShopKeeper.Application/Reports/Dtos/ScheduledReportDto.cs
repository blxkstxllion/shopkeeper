namespace ShopKeeper.Application.Reports.Dtos;

using ShopKeeper.Domain.Entities;

public record ScheduledReportDto(
    Guid Id,
    Guid? BranchId,
    string? BranchName,
    ScheduledReportFrequency Frequency,
    ReportExportFormat Format,
    IReadOnlyList<string> RecipientEmails,
    bool IsActive,
    DateTimeOffset NextRunAt,
    DateTimeOffset? LastRunAt);
