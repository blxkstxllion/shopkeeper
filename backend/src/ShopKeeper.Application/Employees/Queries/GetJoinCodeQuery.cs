namespace ShopKeeper.Application.Employees.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;

public record GetJoinCodeQuery : IRequest<string?>;

public class GetJoinCodeQueryHandler(IAppDbContext db, ICurrentUserService currentUser) : IRequestHandler<GetJoinCodeQuery, string?>
{
    public async Task<string?> Handle(GetJoinCodeQuery request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.EmployeesManage);
        var businessId = currentUser.RequireBusinessId();

        return await db.BusinessSettings.Where(s => s.BusinessId == businessId).Select(s => s.JoinCode).FirstOrDefaultAsync(cancellationToken);
    }
}
