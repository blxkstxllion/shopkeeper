namespace ShopKeeper.Domain.Entities;

using ShopKeeper.Domain.Common;

public enum ScheduledReportFrequency
{
    Daily,
    Weekly,
    Monthly,
}

/// <summary>
/// A recurring "email me the business report" subscription. ScheduledReportRunner (a
/// BackgroundService) polls for rows where NextRunAt has passed, generates the same
/// PDF/Word document GenerateBusinessReportCommand already builds for on-demand exports
/// (never duplicated report logic), and emails it as an attachment.
/// </summary>
public class ScheduledReport : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    /// <summary>Null = all branches, matching the existing report queries' BranchId? filter.</summary>
    public Guid? BranchId { get; set; }
    public Branch? Branch { get; set; }

    public ScheduledReportFrequency Frequency { get; set; }
    public ReportExportFormat Format { get; set; }

    /// <summary>Comma-separated recipient addresses - a handful of emails per schedule, not
    /// enough volume to justify a child table.</summary>
    public string RecipientEmails { get; set; } = default!;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset NextRunAt { get; set; }
    public DateTimeOffset? LastRunAt { get; set; }

    /// <summary>The user whose permissions the scheduled run executes with (see
    /// BackgroundJobContext) - captured at creation time, not re-checked per run, since a
    /// schedule wouldn't have been created without adequate permission in the first place.</summary>
    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = default!;
}

// Mirrors Application/Common/Interfaces/IReportDocumentRenderer.cs's ReportExportFormat -
// redeclared isn't possible (Domain can't reference Application), so ScheduledReport.Format
// uses this Domain-level copy; the export command maps between them 1:1 (same names/order).
public enum ReportExportFormat
{
    Pdf,
    Word,
}
