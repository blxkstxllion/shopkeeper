namespace ShopKeeper.Application.Roles.Commands;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Entities;
using ShopKeeper.Domain.Enums;

public record DeleteRoleCommand(Guid Id) : IRequest;

/// <summary>Soft delete (IsActive = false), not a real DELETE - PendingInvitation.RoleId is
/// OnDelete(Restrict) and invitation rows are kept forever for history (see
/// PendingInvitation's own doc comment), so a hard delete would throw a raw FK violation the
/// moment any invitation, even one accepted years ago, still points at the role. Owner-only,
/// same as Create/UpdateRoleCommand. Not plan-gated - deleting should never be blocked by a
/// downgrade, only creating new roles is.</summary>
public class DeleteRoleCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<DeleteRoleCommand>
{
    public async Task Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsOwner)
        {
            throw new ForbiddenAccessException("Only the business owner can manage roles.");
        }

        var businessId = currentUser.RequireBusinessId();

        var role = await db.Roles
            .FirstOrDefaultAsync(r => r.Id == request.Id && r.BusinessId == businessId && r.IsActive, cancellationToken)
            ?? throw new NotFoundException(nameof(Role), request.Id);

        if (role.IsSystemRole)
        {
            throw new ForbiddenAccessException("Default roles can't be deleted.");
        }

        var employeeCount = await db.BusinessUsers.CountAsync(
            bu => bu.RoleId == role.Id && bu.Status != BusinessUserStatus.Removed, cancellationToken);
        if (employeeCount > 0)
        {
            throw new ConflictException(
                $"{employeeCount} employee{(employeeCount == 1 ? "" : "s")} still use{(employeeCount == 1 ? "s" : "")} this role. Reassign them first.");
        }

        // Filtered in-memory after the AcceptedAt-null narrowing, rather than a single AnyAsync
        // predicate - the SQLite provider (used by the test suite) can't translate a
        // DateTimeOffset.UtcNow comparison combined with the tenant query filter's Guid? cast
        // in this shape. The candidate set here is always small (unaccepted invites for one role).
        var unacceptedInvitations = await db.PendingInvitations
            .Where(i => i.RoleId == role.Id && i.AcceptedAt == null)
            .ToListAsync(cancellationToken);
        var hasPendingInvitation = unacceptedInvitations.Any(i => i.ExpiresAt > DateTimeOffset.UtcNow);
        if (hasPendingInvitation)
        {
            throw new ConflictException("There's a pending invitation for this role. Cancel it or wait for it to expire first.");
        }

        role.IsActive = false;

        await db.SaveChangesAsync(cancellationToken);
    }
}
