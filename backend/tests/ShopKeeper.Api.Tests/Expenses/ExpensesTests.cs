namespace ShopKeeper.Api.Tests.Expenses;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Expenses.Commands;
using ShopKeeper.Application.Expenses.Queries;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;
using ShopKeeper.Infrastructure.Identity;
using ShopKeeper.Infrastructure.Persistence;

public class ExpensesTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));

    private async Task<(PosTestFixture.SeededBusiness Seeded, AppDbContext Context, TestCurrentUserService Owner, Guid CategoryId)> SeedWithCategoryAsync()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var category = await new CreateExpenseCategoryCommandHandler(context, owner).Handle(
            new CreateExpenseCategoryCommand("Rent", null), CancellationToken.None);

        return (seeded, context, owner, category.Id);
    }

    [Fact]
    public async Task CreateExpense_ThenList_ReturnsIt()
    {
        var (seeded, context, owner, categoryId) = await SeedWithCategoryAsync();

        await new CreateExpenseCommandHandler(context, owner).Handle(
            new CreateExpenseCommand(seeded.BranchId, categoryId, 500m, new DateOnly(2026, 8, 1), "August rent"),
            CancellationToken.None);

        var result = await new GetExpensesQueryHandler(context, owner).Handle(
            new GetExpensesQuery(null, null, null, null, 1, 50), CancellationToken.None);

        var expense = Assert.Single(result.Items);
        Assert.Equal(500m, expense.Amount);
        Assert.Equal("Rent", expense.CategoryName);
        Assert.Equal("August rent", expense.Description);
    }

    [Fact]
    public async Task UpdateExpense_PersistsChanges()
    {
        var (seeded, context, owner, categoryId) = await SeedWithCategoryAsync();

        var created = await new CreateExpenseCommandHandler(context, owner).Handle(
            new CreateExpenseCommand(seeded.BranchId, categoryId, 500m, new DateOnly(2026, 8, 1), "Original"),
            CancellationToken.None);

        await new UpdateExpenseCommandHandler(context, owner).Handle(
            new UpdateExpenseCommand(created.Id, seeded.BranchId, categoryId, 650m, new DateOnly(2026, 8, 2), "Corrected"),
            CancellationToken.None);

        var expense = await context.Expenses.SingleAsync(e => e.Id == created.Id);
        Assert.Equal(650m, expense.Amount);
        Assert.Equal(new DateOnly(2026, 8, 2), expense.ExpenseDate);
        Assert.Equal("Corrected", expense.Description);
    }

    [Fact]
    public async Task DeleteExpense_SoftDeletes_ExcludedFromList()
    {
        var (seeded, context, owner, categoryId) = await SeedWithCategoryAsync();

        var created = await new CreateExpenseCommandHandler(context, owner).Handle(
            new CreateExpenseCommand(seeded.BranchId, categoryId, 500m, new DateOnly(2026, 8, 1), null),
            CancellationToken.None);

        await new DeleteExpenseCommandHandler(context, owner).Handle(new DeleteExpenseCommand(created.Id), CancellationToken.None);

        var stillExists = await context.Expenses.IgnoreQueryFilters().AnyAsync(e => e.Id == created.Id);
        Assert.True(stillExists); // never hard-deleted

        var result = await new GetExpensesQueryHandler(context, owner).Handle(
            new GetExpensesQuery(null, null, null, null, 1, 50), CancellationToken.None);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetExpenses_FiltersByDateRange()
    {
        var (seeded, context, owner, categoryId) = await SeedWithCategoryAsync();

        await new CreateExpenseCommandHandler(context, owner).Handle(
            new CreateExpenseCommand(seeded.BranchId, categoryId, 100m, new DateOnly(2026, 7, 15), "July"), CancellationToken.None);
        await new CreateExpenseCommandHandler(context, owner).Handle(
            new CreateExpenseCommand(seeded.BranchId, categoryId, 200m, new DateOnly(2026, 8, 15), "August"), CancellationToken.None);

        var result = await new GetExpensesQueryHandler(context, owner).Handle(
            new GetExpensesQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), null, null, 1, 50), CancellationToken.None);

        var expense = Assert.Single(result.Items);
        Assert.Equal("August", expense.Description);
    }

    [Fact]
    public async Task GetExpenses_ScopedToOneBranch_ExcludesOtherBranch()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var branchB = new Branch { BusinessId = seeded.BusinessId, Name = "Branch B", Code = "B2", Country = "Ghana" };
        context.Branches.Add(branchB);
        await context.SaveChangesAsync(CancellationToken.None);

        var category = await new CreateExpenseCategoryCommandHandler(context, owner).Handle(
            new CreateExpenseCategoryCommand("Utilities", null), CancellationToken.None);

        await new CreateExpenseCommandHandler(context, owner).Handle(
            new CreateExpenseCommand(seeded.BranchId, category.Id, 100m, new DateOnly(2026, 8, 1), "Branch A"), CancellationToken.None);
        await new CreateExpenseCommandHandler(context, owner).Handle(
            new CreateExpenseCommand(branchB.Id, category.Id, 200m, new DateOnly(2026, 8, 1), "Branch B"), CancellationToken.None);

        var result = await new GetExpensesQueryHandler(context, owner).Handle(
            new GetExpensesQuery(null, null, null, branchB.Id, 1, 50), CancellationToken.None);

        var expense = Assert.Single(result.Items);
        Assert.Equal("Branch B", expense.Description);
    }

    [Fact]
    public async Task BranchManager_CanViewButNotCreateExpenses()
    {
        var (seeded, context, _, categoryId) = await SeedWithCategoryAsync();

        var branchManager = new TestCurrentUserService
        {
            UserId = Guid.NewGuid(),
            BusinessId = seeded.BusinessId,
            BranchId = seeded.BranchId,
            IsOwner = false,
            PermissionsList = DefaultRoles.RolePermissionKeys[DefaultRoles.BranchManager].ToList(),
        };

        // View works - BranchManager has expenses:view.
        var result = await new GetExpensesQueryHandler(context, branchManager).Handle(
            new GetExpensesQuery(null, null, null, null, 1, 50), CancellationToken.None);
        Assert.Empty(result.Items);

        // Create does not - BranchManager lacks expenses:manage.
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => new CreateExpenseCommandHandler(context, branchManager).Handle(
            new CreateExpenseCommand(seeded.BranchId, categoryId, 100m, new DateOnly(2026, 8, 1), null), CancellationToken.None));
    }

    public void Dispose() => _db.Dispose();
}
