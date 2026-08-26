namespace ShopKeeper.Application.Auth.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Auth.Dtos;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Interfaces;

public record GetCurrentUserQuery(Guid UserId) : IRequest<UserDto>;

public class GetCurrentUserQueryHandler(IAppDbContext db) : IRequestHandler<GetCurrentUserQuery, UserDto>
{
    public async Task<UserDto> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.User), request.UserId);

        var memberships = await db.BusinessUsers
            .IgnoreQueryFilters()
            .Where(bu => bu.UserId == user.Id && bu.Status != Domain.Enums.BusinessUserStatus.Removed)
            .Include(bu => bu.Business)
            .Include(bu => bu.Role)
            .ToListAsync(cancellationToken);

        return new UserDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.IsEmailVerified,
            memberships.Select(m => new UserBusinessDto(
                m.BusinessId, m.Business.Name, m.Role.Name, m.IsOwner, m.Business.OnboardingCompleted,
                m.Business.CurrencyCode, m.Business.ColorTheme)).ToList());
    }
}
