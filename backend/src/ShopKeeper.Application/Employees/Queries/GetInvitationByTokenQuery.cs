namespace ShopKeeper.Application.Employees.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Employees.Dtos;
using ShopKeeper.Domain.Entities;

/// <summary>Unauthenticated - the invitee isn't logged in yet when they land on the accept-invite
/// page, so this can't go through the normal tenant-scoped query path.</summary>
public record GetInvitationByTokenQuery(string Token) : IRequest<InvitationDetailsDto>;

public class GetInvitationByTokenQueryHandler(IAppDbContext db) : IRequestHandler<GetInvitationByTokenQuery, InvitationDetailsDto>
{
    public async Task<InvitationDetailsDto> Handle(GetInvitationByTokenQuery request, CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters: the caller has no active business (they aren't authenticated yet),
        // so the normal tenant filter would match nothing - this is the one deliberately
        // cross-tenant lookup an invitee needs before they've joined any business.
        var invitation = await db.PendingInvitations
            .IgnoreQueryFilters()
            .Include(i => i.Business)
            .Include(i => i.Role)
            .Include(i => i.InvitedByUser)
            .FirstOrDefaultAsync(i => i.Token == request.Token, cancellationToken)
            ?? throw new NotFoundException(nameof(PendingInvitation), request.Token);

        if (invitation.AcceptedAt is not null)
        {
            throw new ConflictException("This invitation has already been accepted.");
        }

        if (invitation.ExpiresAt < DateTimeOffset.UtcNow)
        {
            throw new ConflictException("This invitation has expired.");
        }

        var userExists = await db.Users.AnyAsync(u => u.Email == invitation.Email, cancellationToken);

        return new InvitationDetailsDto(
            invitation.BusinessId,
            invitation.Email,
            invitation.Business.Name,
            $"{invitation.InvitedByUser.FirstName} {invitation.InvitedByUser.LastName}",
            invitation.Role.Name,
            userExists);
    }
}
