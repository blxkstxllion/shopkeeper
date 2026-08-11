namespace ShopKeeper.Application.Auth.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Interfaces;

public record TwoFactorStatusDto(bool Enabled);

public record GetTwoFactorStatusQuery(Guid UserId) : IRequest<TwoFactorStatusDto>;

public class GetTwoFactorStatusQueryHandler(IAppDbContext db) : IRequestHandler<GetTwoFactorStatusQuery, TwoFactorStatusDto>
{
    public async Task<TwoFactorStatusDto> Handle(GetTwoFactorStatusQuery request, CancellationToken cancellationToken)
    {
        var enabled = await db.Users.Where(u => u.Id == request.UserId).Select(u => u.TwoFactorEnabled).FirstAsync(cancellationToken);
        return new TwoFactorStatusDto(enabled);
    }
}
