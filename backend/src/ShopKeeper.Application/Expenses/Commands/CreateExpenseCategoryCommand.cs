namespace ShopKeeper.Application.Expenses.Commands;

using FluentValidation;
using MediatR;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Expenses.Dtos;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;

public record CreateExpenseCategoryCommand(string Name, string? Description) : IRequest<ExpenseCategoryDto>;

public class CreateExpenseCategoryCommandValidator : AbstractValidator<CreateExpenseCategoryCommand>
{
    public CreateExpenseCategoryCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    }
}

public class CreateExpenseCategoryCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateExpenseCategoryCommand, ExpenseCategoryDto>
{
    public async Task<ExpenseCategoryDto> Handle(CreateExpenseCategoryCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.ExpensesManage);
        var businessId = currentUser.RequireBusinessId();

        var category = new ExpenseCategory
        {
            BusinessId = businessId,
            Name = request.Name.Trim(),
            Description = request.Description,
        };

        db.ExpenseCategories.Add(category);
        await db.SaveChangesAsync(cancellationToken);

        return new ExpenseCategoryDto(category.Id, category.Name, category.Description, category.IsActive);
    }
}
