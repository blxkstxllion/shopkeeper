namespace ShopKeeper.Application.Businesses.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Businesses.Dtos;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;

public record CreateBranchCommand(
    string Name,
    string Code,
    string? Address,
    string? City,
    string? Country,
    string? Phone,
    string? Email) : IRequest<BranchDto>;

public class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
    }
}

public class CreateBranchCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateBranchCommand, BranchDto>
{
    public async Task<BranchDto> Handle(CreateBranchCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.BranchesManage);
        var businessId = currentUser.RequireBusinessId();

        var codeTaken = await db.Branches.AnyAsync(b => b.Code == request.Code, cancellationToken);
        if (codeTaken)
        {
            throw new ConflictException($"A branch with code '{request.Code}' already exists.");
        }

        var branch = new Branch
        {
            BusinessId = businessId,
            Name = request.Name.Trim(),
            Code = request.Code.Trim(),
            Address = request.Address,
            City = request.City,
            Country = request.Country,
            Phone = request.Phone,
            Email = request.Email,
            IsMainBranch = false,
        };

        db.Branches.Add(branch);
        await db.SaveChangesAsync(cancellationToken);

        return new BranchDto(
            branch.Id, branch.Name, branch.Code, branch.Address, branch.City, branch.Country,
            branch.Phone, branch.Email, branch.IsMainBranch, branch.IsActive);
    }
}
