namespace ShopKeeper.Application.Employees.Commands;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;

public record RevokeJoinCodeCommand : IRequest;

public class RevokeJoinCodeCommandHandler(IAppDbContext db, ICurrentUserService currentUser) : IRequestHandler<RevokeJoinCodeCommand>
{
    public async Task Handle(RevokeJoinCodeCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.EmployeesManage);
        var businessId = currentUser.RequireBusinessId();

        var setting = await db.BusinessSettings.FirstOrDefaultAsync(s => s.BusinessId == businessId, cancellationToken)
            ?? throw new NotFoundException(nameof(BusinessSetting), businessId);

        setting.JoinCode = null;
        await db.SaveChangesAsync(cancellationToken);
    }
}
