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

public record CreateRoleCommand(string Name, string? Description, List<string> PermissionKeys)
    : IRequest<Guid>, IRequirePlanFeature
{
    public bool RequiresReports => false;
    public bool RequiresAi => false;
    public bool RequiresCustomRoles => true;
}

public class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleForEach(x => x.PermissionKeys)
            .Must(key => PermissionKeys.All.Any(p => p.Key == key))
            .WithMessage("'{PropertyValue}' is not a recognized permission.");
    }
}

/// <summary>Owner-only rather than a permission key - see SetPlanTierCommand for why this
/// codebase treats ownership as the gate for org-structural decisions rather than
/// backfilling a new permission onto every already-onboarded business's roles.</summary>
public class CreateRoleCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateRoleCommand, Guid>
{
    public async Task<Guid> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsOwner)
        {
            throw new ForbiddenAccessException("Only the business owner can manage roles.");
        }

        var businessId = currentUser.RequireBusinessId();

        var nameTaken = await db.Roles.AnyAsync(
            r => r.BusinessId == businessId && r.IsActive && r.Name == request.Name, cancellationToken);
        if (nameTaken)
        {
            throw new ConflictException($"A role named '{request.Name}' already exists.");
        }

        var permissions = await db.Permissions
            .Where(p => request.PermissionKeys.Contains(p.Key))
            .ToListAsync(cancellationToken);

        var role = new Role
        {
            BusinessId = businessId,
            Name = request.Name.Trim(),
            Description = request.Description,
            IsSystemRole = false,
            IsActive = true,
        };

        foreach (var permission in permissions)
        {
            role.RolePermissions.Add(new RolePermission { Role = role, Permission = permission });
        }

        db.Roles.Add(role);

        await db.SaveChangesAsync(cancellationToken);

        return role.Id;
    }
}
