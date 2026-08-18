namespace ShopKeeper.Api.Tests.Roles;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Auth.Commands;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Application.Employees.Commands;
using ShopKeeper.Application.Roles.Commands;
using ShopKeeper.Application.Roles.Queries;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Infrastructure.Identity;

public class RoleManagementTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));
    private readonly TestEmailSender _emailSender = new();

    [Fact]
    public async Task CreateRole_ByOwner_CreatesCustomRoleWithPermissions()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var roleId = await new CreateRoleCommandHandler(context, owner).Handle(
            new CreateRoleCommand("Night Shift Lead", "Closes the store", [PermissionKeys.SalesView, PermissionKeys.SalesCreate]),
            CancellationToken.None);

        var role = await context.Roles.Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission).SingleAsync(r => r.Id == roleId);
        Assert.Equal("Night Shift Lead", role.Name);
        Assert.False(role.IsSystemRole);
        Assert.True(role.IsActive);
        Assert.Equal(2, role.RolePermissions.Count);
        Assert.Contains(role.RolePermissions, rp => rp.Permission.Key == PermissionKeys.SalesView);
    }

    [Fact]
    public async Task CreateRole_ByNonOwner_ThrowsForbidden()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var context = _db.CreateContext(seeded.AsOwner());

        var administrator = new TestCurrentUserService
        {
            UserId = Guid.NewGuid(),
            BusinessId = seeded.BusinessId,
            IsOwner = false,
            PermissionsList = DefaultRoles.RolePermissionKeys[DefaultRoles.Administrator].ToList(),
        };

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => new CreateRoleCommandHandler(context, administrator).Handle(
            new CreateRoleCommand("Shadow Admin", null, []), CancellationToken.None));
    }

    [Fact]
    public async Task CreateRole_DuplicateName_ThrowsConflict()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        await new CreateRoleCommandHandler(context, owner).Handle(
            new CreateRoleCommand("Trainee", null, []), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => new CreateRoleCommandHandler(context, owner).Handle(
            new CreateRoleCommand("Trainee", null, []), CancellationToken.None));
    }

    [Fact]
    public async Task CreateRole_UnknownPermissionKey_IsSilentlyIgnored()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var roleId = await new CreateRoleCommandHandler(context, owner).Handle(
            new CreateRoleCommand("Trainee", null, ["not:a:real:key", PermissionKeys.SalesView]), CancellationToken.None);

        var role = await context.Roles.Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission).SingleAsync(r => r.Id == roleId);
        Assert.Single(role.RolePermissions);
        Assert.Equal(PermissionKeys.SalesView, role.RolePermissions.Single().Permission.Key);
    }

    [Fact]
    public void CreateRoleValidator_UnknownPermissionKey_FailsValidation()
    {
        var result = new CreateRoleCommandValidator().Validate(
            new CreateRoleCommand("Trainee", null, ["not:a:real:key"]));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task UpdateRole_ByOwner_ReplacesPermissions()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var roleId = await new CreateRoleCommandHandler(context, owner).Handle(
            new CreateRoleCommand("Trainee", null, [PermissionKeys.SalesView]), CancellationToken.None);

        await new UpdateRoleCommandHandler(context, owner).Handle(
            new UpdateRoleCommand(roleId, "Senior Trainee", "Promoted", [PermissionKeys.InventoryView, PermissionKeys.InventoryModify]),
            CancellationToken.None);

        var role = await context.Roles.Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission).SingleAsync(r => r.Id == roleId);
        Assert.Equal("Senior Trainee", role.Name);
        Assert.Equal("Promoted", role.Description);
        Assert.Equal(2, role.RolePermissions.Count);
        Assert.DoesNotContain(role.RolePermissions, rp => rp.Permission.Key == PermissionKeys.SalesView);
    }

    [Theory]
    [InlineData(DefaultRoles.Owner)]
    [InlineData(DefaultRoles.Cashier)]
    public async Task UpdateRole_SystemRole_ThrowsForbidden(string systemRoleName)
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var systemRole = await context.Roles.SingleAsync(r => r.Name == systemRoleName);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => new UpdateRoleCommandHandler(context, owner).Handle(
            new UpdateRoleCommand(systemRole.Id, "Renamed", null, []), CancellationToken.None));

        var unchanged = await context.Roles.SingleAsync(r => r.Id == systemRole.Id);
        Assert.Equal(systemRoleName, unchanged.Name);
    }

    [Fact]
    public async Task UpdateRole_ByNonOwner_ThrowsForbidden()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var roleId = await new CreateRoleCommandHandler(context, owner).Handle(
            new CreateRoleCommand("Trainee", null, []), CancellationToken.None);

        var administrator = new TestCurrentUserService
        {
            UserId = Guid.NewGuid(),
            BusinessId = seeded.BusinessId,
            IsOwner = false,
            PermissionsList = DefaultRoles.RolePermissionKeys[DefaultRoles.Administrator].ToList(),
        };

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => new UpdateRoleCommandHandler(context, administrator).Handle(
            new UpdateRoleCommand(roleId, "Hijacked", null, []), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateRole_OnFreeTierBusiness_StillSucceeds()
    {
        // UpdateRoleCommand is deliberately not plan-gated (IRequirePlanFeature) - an existing
        // custom role must stay editable even for a business that's never been on Enterprise
        // (or downgraded out of it). Direct handler construction bypasses the MediatR pipeline
        // entirely anyway, but this documents the intent explicitly rather than relying on that
        // as an accident of the test style.
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var business = await context.Businesses.SingleAsync(b => b.Id == seeded.BusinessId);
        Assert.Equal(Domain.Enums.PlanTier.Free, business.PlanTier); // sanity: onboarding defaults to Free

        var roleId = await new CreateRoleCommandHandler(context, owner).Handle(
            new CreateRoleCommand("Trainee", null, []), CancellationToken.None);

        await new UpdateRoleCommandHandler(context, owner).Handle(
            new UpdateRoleCommand(roleId, "Trainee", "Updated on Free tier", []), CancellationToken.None);

        var role = await context.Roles.SingleAsync(r => r.Id == roleId);
        Assert.Equal("Updated on Free tier", role.Description);
    }

    [Theory]
    [InlineData(DefaultRoles.Owner)]
    [InlineData(DefaultRoles.Administrator)]
    public async Task DeleteRole_SystemRole_ThrowsForbidden(string systemRoleName)
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var systemRole = await context.Roles.SingleAsync(r => r.Name == systemRoleName);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => new DeleteRoleCommandHandler(context, owner).Handle(
            new DeleteRoleCommand(systemRole.Id), CancellationToken.None));

        var stillThere = await context.Roles.SingleAsync(r => r.Id == systemRole.Id);
        Assert.True(stillThere.IsActive);
    }

    [Fact]
    public async Task DeleteRole_ByNonOwner_ThrowsForbidden()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var roleId = await new CreateRoleCommandHandler(context, owner).Handle(
            new CreateRoleCommand("Trainee", null, []), CancellationToken.None);

        var administrator = new TestCurrentUserService
        {
            UserId = Guid.NewGuid(),
            BusinessId = seeded.BusinessId,
            IsOwner = false,
            PermissionsList = DefaultRoles.RolePermissionKeys[DefaultRoles.Administrator].ToList(),
        };

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => new DeleteRoleCommandHandler(context, administrator).Handle(
            new DeleteRoleCommand(roleId), CancellationToken.None));
    }

    [Fact]
    public async Task DeleteRole_WithAssignedEmployee_ThrowsConflict_ThenSucceedsOnceRemoved()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var tokenIssuer = new TokenIssuer(context, _jwt);

        var roleId = await new CreateRoleCommandHandler(context, owner).Handle(
            new CreateRoleCommand("Trainee", null, [PermissionKeys.SalesView]), CancellationToken.None);

        await new InviteEmployeeCommandHandler(context, owner, _emailSender, _jwt).Handle(
            new InviteEmployeeCommand("trainee@shop.test", roleId, null), CancellationToken.None);
        var token = _emailSender.LastInvite!.Value.InviteToken;
        await new AcceptInvitationCommandHandler(context, _hasher, tokenIssuer, _jwt, new PlanLimitService(context)).Handle(
            new AcceptInvitationCommand(token, "Passw0rd!", "Kofi", "Mensah", null), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => new DeleteRoleCommandHandler(context, owner).Handle(
            new DeleteRoleCommand(roleId), CancellationToken.None));
        Assert.Contains("employee", ex.Message);

        var membership = await context.BusinessUsers.IgnoreQueryFilters()
            .SingleAsync(bu => bu.BusinessId == seeded.BusinessId && bu.User.Email == "trainee@shop.test");
        await new RemoveEmployeeCommandHandler(context, owner).Handle(new RemoveEmployeeCommand(membership.Id), CancellationToken.None);

        await new DeleteRoleCommandHandler(context, owner).Handle(new DeleteRoleCommand(roleId), CancellationToken.None);
        var role = await context.Roles.SingleAsync(r => r.Id == roleId);
        Assert.False(role.IsActive);
    }

    [Fact]
    public async Task DeleteRole_WithPendingInvitation_ThrowsConflict()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var roleId = await new CreateRoleCommandHandler(context, owner).Handle(
            new CreateRoleCommand("Trainee", null, []), CancellationToken.None);

        await new InviteEmployeeCommandHandler(context, owner, _emailSender, _jwt).Handle(
            new InviteEmployeeCommand("invited@shop.test", roleId, null), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => new DeleteRoleCommandHandler(context, owner).Handle(
            new DeleteRoleCommand(roleId), CancellationToken.None));
        Assert.Contains("pending invitation", ex.Message);
    }

    [Fact]
    public async Task DeleteRole_NoUsage_SetsInactive_AndExcludedFromManagementQuery()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var roleId = await new CreateRoleCommandHandler(context, owner).Handle(
            new CreateRoleCommand("Trainee", null, []), CancellationToken.None);

        await new DeleteRoleCommandHandler(context, owner).Handle(new DeleteRoleCommand(roleId), CancellationToken.None);

        var role = await context.Roles.SingleAsync(r => r.Id == roleId);
        Assert.False(role.IsActive);

        var management = await new GetRoleManagementQueryHandler(context, owner).Handle(new GetRoleManagementQuery(), CancellationToken.None);
        Assert.DoesNotContain(management, r => r.Id == roleId);
    }

    [Fact]
    public async Task RoleChanges_NeverAffectAnotherBusiness()
    {
        var businessA = await PosTestFixture.SeedAsync(_db, _hasher, _jwt, "ownerA@shop.test");
        var businessB = await PosTestFixture.SeedAsync(_db, _hasher, _jwt, "ownerB@shop.test");
        var ownerA = businessA.AsOwner();
        var ownerB = businessB.AsOwner();
        var contextA = _db.CreateContext(ownerA);
        var contextB = _db.CreateContext(ownerB);

        await new CreateRoleCommandHandler(contextA, ownerA).Handle(
            new CreateRoleCommand("Shared Name", null, []), CancellationToken.None);

        // Business B can create a role with the identical name - the unique index is scoped
        // per-business, and the roleId lookups below must never cross tenants.
        var roleIdB = await new CreateRoleCommandHandler(contextB, ownerB).Handle(
            new CreateRoleCommand("Shared Name", null, []), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() => new UpdateRoleCommandHandler(contextA, ownerA).Handle(
            new UpdateRoleCommand(roleIdB, "Hijacked", null, []), CancellationToken.None));

        var roleB = await contextB.Roles.SingleAsync(r => r.Id == roleIdB);
        Assert.Equal("Shared Name", roleB.Name);
    }

    [Fact]
    public async Task GetRoleManagement_ReturnsAllActiveRolesWithEmployeeCounts()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var result = await new GetRoleManagementQueryHandler(context, owner).Handle(new GetRoleManagementQuery(), CancellationToken.None);

        Assert.Equal(7, result.Count); // the 7 seeded defaults
        var ownerRow = result.Single(r => r.Name == DefaultRoles.Owner);
        Assert.True(ownerRow.IsSystemRole);
        Assert.Equal(1, ownerRow.EmployeeCount); // the seeded owner
        Assert.Contains(PermissionKeys.SettingsManage, ownerRow.PermissionKeys);
    }

    public void Dispose() => _db.Dispose();
}
