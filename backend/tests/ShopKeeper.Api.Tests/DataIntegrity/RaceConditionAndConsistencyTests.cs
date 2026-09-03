namespace ShopKeeper.Api.Tests.DataIntegrity;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Auth.Commands;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Application.Employees.Commands;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;
using ShopKeeper.Infrastructure.Identity;

/// <summary>
/// Proves the check-then-insert races from the code review (#6) are actually closed by a real
/// database constraint, not just the application-level precheck - and that the sync/async
/// SaveChanges timestamp gap (#7) is closed. Uses ConcurrentSqliteTestDatabase (independent
/// connections onto one shared-cache SQLite database) for genuine Task.WhenAll races, same
/// helper and reasoning as the inventory-concurrency tests.
/// </summary>
public class RaceConditionAndConsistencyTests
{
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));

    [Fact]
    public async Task Register_ConcurrentDuplicateRegistrations_ExactlyOneSucceeds()
    {
        using var db = new ConcurrentSqliteTestDatabase();

        async Task<bool> TryRegister()
        {
            var context = db.CreateContext(new TestCurrentUserService());
            var tokenIssuer = new TokenIssuer(context, _jwt);
            try
            {
                await new RegisterCommandHandler(context, _hasher, tokenIssuer, new TestEmailSender()).Handle(
                    new RegisterCommand("race@shop.test", "Passw0rd!", "Ama", "Owusu", null), CancellationToken.None);
                return true;
            }
            catch (ConflictException)
            {
                return false;
            }
        }

        var results = await Task.WhenAll(TryRegister(), TryRegister());

        Assert.Single(results, r => r); // exactly one registration won
        Assert.Single(results, r => !r); // the other got a clean ConflictException, not a raw 500

        var setupContext = db.CreateContext(new TestCurrentUserService());
        var count = await setupContext.Users.AsNoTracking().CountAsync(u => u.Email == "race@shop.test");
        Assert.Equal(1, count); // the unique index is what actually stopped the duplicate
    }

    [Fact]
    public async Task InviteEmployee_ConcurrentDuplicateInvites_ExactlyOneSucceeds()
    {
        using var db = new ConcurrentSqliteTestDatabase();
        var seeded = await PosTestFixture.SeedAsync(db, _hasher, _jwt);
        var owner = seeded.AsOwner();

        var setupContext = db.CreateContext(owner);
        var cashierRoleId = await setupContext.Roles
            .Where(r => r.Name == DefaultRoles.Cashier).Select(r => r.Id).SingleAsync();

        async Task<bool> TryInvite()
        {
            var context = db.CreateContext(owner);
            try
            {
                await new InviteEmployeeCommandHandler(context, owner, new TestEmailSender(), _jwt).Handle(
                    new InviteEmployeeCommand("racehire@shop.test", cashierRoleId, null), CancellationToken.None);
                return true;
            }
            catch (ConflictException)
            {
                return false;
            }
        }

        var results = await Task.WhenAll(TryInvite(), TryInvite());

        Assert.Single(results, r => r);
        Assert.Single(results, r => !r);

        var count = await setupContext.PendingInvitations.AsNoTracking()
            .CountAsync(i => i.Email == "racehire@shop.test" && i.AcceptedAt == null);
        Assert.Equal(1, count); // the partial unique index is what actually stopped the duplicate
    }

    [Fact]
    public async Task SaveChanges_SyncPath_StampsTimestampsSameAsAsyncPath()
    {
        using var db = new SqliteTestDatabase();
        var seeded = await PosTestFixture.SeedAsync(db, _hasher, _jwt);
        var context = db.CreateContext(seeded.AsOwner());

        var supplier = new Supplier { BusinessId = seeded.BusinessId, Name = "Sync Path Supplier" };
        context.Suppliers.Add(supplier);

        var before = DateTimeOffset.UtcNow;
        context.SaveChanges();
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(supplier.CreatedAt, before, after);
        Assert.InRange(supplier.UpdatedAt, before, after);

        supplier.Name = "Renamed via sync path";
        var beforeUpdate = DateTimeOffset.UtcNow;
        context.SaveChanges();
        var afterUpdate = DateTimeOffset.UtcNow;

        Assert.InRange(supplier.UpdatedAt, beforeUpdate, afterUpdate);
        Assert.True(supplier.UpdatedAt > supplier.CreatedAt);
    }

    [Fact]
    public async Task RemoveEmployee_ThenLogin_BusinessNoLongerAppearsInMemberships()
    {
        using var db = new SqliteTestDatabase();
        var seeded = await PosTestFixture.SeedAsync(db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = db.CreateContext(owner);
        var tokenIssuer = new TokenIssuer(context, _jwt);
        var cashierRoleId = await context.Roles.Where(r => r.Name == DefaultRoles.Cashier).Select(r => r.Id).SingleAsync();

        var emailSender = new TestEmailSender();
        await new InviteEmployeeCommandHandler(context, owner, emailSender, _jwt).Handle(
            new InviteEmployeeCommand("removeme@shop.test", cashierRoleId, null), CancellationToken.None);
        var token = emailSender.LastInvite!.Value.InviteToken;
        await new AcceptInvitationCommandHandler(context, _hasher, tokenIssuer, _jwt, new PlanLimitService(context)).Handle(
            new AcceptInvitationCommand(token, "Passw0rd!", "Kofi", "Mensah", null), CancellationToken.None);

        var membership = await context.BusinessUsers.IgnoreQueryFilters()
            .SingleAsync(bu => bu.BusinessId == seeded.BusinessId && bu.User.Email == "removeme@shop.test");
        await new RemoveEmployeeCommandHandler(context, owner).Handle(new RemoveEmployeeCommand(membership.Id), CancellationToken.None);

        // Removed user cannot obtain a *new* token scoped to this business - even though any
        // access token issued before removal remains valid for its remaining ~15 minutes
        // (documented tradeoff in TokenIssuer.IssueAsync, finding #9), a fresh login must not
        // include the business they were just removed from.
        var loginHandler = new LoginCommandHandler(context, _hasher, tokenIssuer, _jwt);
        var loginResult = await loginHandler.Handle(
            new LoginCommand("removeme@shop.test", "Passw0rd!", null, false, null), CancellationToken.None);

        Assert.False(loginResult.RequiresTwoFactor);
        Assert.DoesNotContain(loginResult.Auth!.User.Businesses, b => b.BusinessId == seeded.BusinessId);
    }
}
