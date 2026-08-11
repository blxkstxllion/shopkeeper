namespace ShopKeeper.Application.Businesses.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;

public record UpdateBranchCommand(
    Guid Id,
    string Name,
    string Code,
    string? Address,
    string? City,
    string? Country,
    string? Phone,
    string? Email,
    bool IsMain,
    bool IsActive) : IRequest;

public class UpdateBranchCommandValidator : AbstractValidator<UpdateBranchCommand>
{
    public UpdateBranchCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
    }
}

/// <summary>
/// Two rules with no existing precedent elsewhere in the codebase: a business always needs at
/// least one active branch (everything - sales, inventory - is scoped through one), and the main
/// branch can't be deactivated or demoted directly - promoting a different branch to main is the
/// only way to change which one holds that status, so there's never a moment with zero or two
/// main branches.
/// </summary>
public class UpdateBranchCommandHandler(IAppDbContext db, ICurrentUserService currentUser) : IRequestHandler<UpdateBranchCommand>
{
    public async Task Handle(UpdateBranchCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.BranchesManage);

        var branch = await db.Branches.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Branch), request.Id);

        var codeTaken = await db.Branches.AnyAsync(b => b.Id != request.Id && b.Code == request.Code, cancellationToken);
        if (codeTaken)
        {
            throw new ConflictException($"A branch with code '{request.Code}' already exists.");
        }

        if (!request.IsActive && branch.IsActive)
        {
            var otherActiveCount = await db.Branches.CountAsync(b => b.Id != request.Id && b.IsActive, cancellationToken);
            if (otherActiveCount == 0)
            {
                throw new ConflictException("Can't deactivate the only active branch.");
            }
            if (branch.IsMainBranch)
            {
                throw new ConflictException("Can't deactivate the main branch. Promote another branch to main first.");
            }
        }

        if (!request.IsMain && branch.IsMainBranch)
        {
            throw new ConflictException("Can't remove main-branch status directly. Promote another branch to main instead.");
        }

        if (request.IsMain && !branch.IsMainBranch)
        {
            var currentMain = await db.Branches.FirstOrDefaultAsync(b => b.IsMainBranch && b.Id != request.Id, cancellationToken);
            if (currentMain is not null)
            {
                currentMain.IsMainBranch = false;
            }
            branch.IsMainBranch = true;
        }

        branch.Name = request.Name.Trim();
        branch.Code = request.Code.Trim();
        branch.Address = request.Address;
        branch.City = request.City;
        branch.Country = request.Country;
        branch.Phone = request.Phone;
        branch.Email = request.Email;
        branch.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}
