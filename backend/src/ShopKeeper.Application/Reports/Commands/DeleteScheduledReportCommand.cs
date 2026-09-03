namespace ShopKeeper.Application.Reports.Commands;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;

public record DeleteScheduledReportCommand(Guid Id) : IRequest;

public class DeleteScheduledReportCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<DeleteScheduledReportCommand>
{
    public async Task Handle(DeleteScheduledReportCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.ReportsView);

        var report = await db.ScheduledReports.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ScheduledReport), request.Id);

        db.ScheduledReports.Remove(report);
        await db.SaveChangesAsync(cancellationToken);
    }
}
