namespace ShopKeeper.Application.Roles.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Behaviors;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;

public record UpdateRoleCommand(Guid Id, string Name, string? Description, List<string> PermissionKeys, Guid? ClientRequestId = null)
    : IRequest, ISupportsClientRequestId;

public class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleForEach(x => x.PermissionKeys)
            .Must(key => PermissionKeys.All.Any(p => p.Key == key))
            .WithMessage("'{PropertyValue}' is not a recognized permission.");
    }
}

/// <summary>Owner-only, same as CreateRoleCommand. Not plan-gated (IRequirePlanFeature) -
/// a business that downgrades out of Enterprise keeps its existing custom roles fully
/// editable, matching the "downgrade never touches existing resources" philosophy from
/// PlanLimitService. System roles (the 7 defaults) can never be edited, on any tier.</summary>
public class UpdateRoleCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<UpdateRoleCommand>
{
    public async Task Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsOwner)
        {
            throw new ForbiddenAccessException("Only the business owner can manage roles.");
        }

        var businessId = currentUser.RequireBusinessId();

        var role = await db.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == request.Id && r.BusinessId == businessId && r.IsActive, cancellationToken)
            ?? throw new NotFoundException(nameof(Role), request.Id);

        if (role.IsSystemRole)
        {
            throw new ForbiddenAccessException("Default roles can't be edited. Create a custom role instead.");
        }

        var nameTaken = await db.Roles.AnyAsync(
            r => r.BusinessId == businessId && r.IsActive && r.Id != role.Id && r.Name == request.Name, cancellationToken);
        if (nameTaken)
        {
            throw new ConflictException($"A role named '{request.Name}' already exists.");
        }

        var permissions = await db.Permissions
            .Where(p => request.PermissionKeys.Contains(p.Key))
            .ToListAsync(cancellationToken);

        role.Name = request.Name.Trim();
        role.Description = request.Description;

        role.RolePermissions.Clear();
        foreach (var permission in permissions)
        {
            role.RolePermissions.Add(new RolePermission { Role = role, Permission = permission });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
