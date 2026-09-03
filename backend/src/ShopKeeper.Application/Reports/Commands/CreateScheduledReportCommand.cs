namespace ShopKeeper.Application.Reports.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Behaviors;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Reports.Dtos;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;
using ReportExportFormat = ShopKeeper.Domain.Entities.ReportExportFormat;

public record CreateScheduledReportCommand(
    Guid? BranchId,
    ScheduledReportFrequency Frequency,
    ReportExportFormat Format,
    IReadOnlyList<string> RecipientEmails) : IRequest<ScheduledReportDto>, IRequirePlanFeature
{
    public bool RequiresReports => true;
    public bool RequiresAi => false;
    public bool RequiresCustomRoles => false;
}

public class CreateScheduledReportCommandValidator : AbstractValidator<CreateScheduledReportCommand>
{
    public CreateScheduledReportCommandValidator()
    {
        RuleFor(x => x.RecipientEmails).NotEmpty().WithMessage("At least one recipient email is required.");
        RuleForEach(x => x.RecipientEmails).EmailAddress();
        RuleFor(x => x.RecipientEmails.Count).LessThanOrEqualTo(10).WithMessage("At most 10 recipients per schedule.");
    }
}

public class CreateScheduledReportCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateScheduledReportCommand, ScheduledReportDto>
{
    public async Task<ScheduledReportDto> Handle(CreateScheduledReportCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.ReportsView);
        if (request.BranchId.HasValue)
        {
            currentUser.RequireBranchAccess(request.BranchId.Value);
        }
        var businessId = currentUser.RequireBusinessId();

        var report = new ScheduledReport
        {
            BusinessId = businessId,
            BranchId = request.BranchId,
            Frequency = request.Frequency,
            Format = request.Format,
            RecipientEmails = string.Join(',', request.RecipientEmails.Select(e => e.Trim().ToLowerInvariant())),
            CreatedByUserId = currentUser.RequireUserId(),
            NextRunAt = ScheduledReportScheduling.NextRunAfter(DateTimeOffset.UtcNow, request.Frequency),
        };
        db.ScheduledReports.Add(report);
        await db.SaveChangesAsync(cancellationToken);

        var branchName = request.BranchId.HasValue
            ? await db.Branches.Where(b => b.Id == request.BranchId).Select(b => b.Name).FirstOrDefaultAsync(cancellationToken)
            : null;

        return new ScheduledReportDto(
            report.Id, report.BranchId, branchName, report.Frequency, report.Format,
            request.RecipientEmails, report.IsActive, report.NextRunAt, report.LastRunAt);
    }
}
