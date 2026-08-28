namespace ShopKeeper.Application.Employees.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Employees.Dtos;
using ShopKeeper.Domain.Entities;

/// <summary>Unauthenticated - a visitor scanning/typing a join code isn't logged into any
/// business yet, so this can't go through the normal tenant-scoped query path.</summary>
public record GetBusinessByJoinCodeQuery(string Code) : IRequest<JoinBusinessDto>;

public class GetBusinessByJoinCodeQueryHandler(IAppDbContext db) : IRequestHandler<GetBusinessByJoinCodeQuery, JoinBusinessDto>
{
    public async Task<JoinBusinessDto> Handle(GetBusinessByJoinCodeQuery request, CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters: same reasoning as GetInvitationByTokenQuery - the visitor has no
        // active business yet, so the normal tenant filter would match nothing.
        var setting = await db.BusinessSettings
            .IgnoreQueryFilters()
            .Include(s => s.Business)
            .FirstOrDefaultAsync(s => s.JoinCode == request.Code, cancellationToken)
            ?? throw new NotFoundException(nameof(BusinessSetting), request.Code);

        return new JoinBusinessDto(setting.BusinessId, setting.Business.Name);
    }
}
