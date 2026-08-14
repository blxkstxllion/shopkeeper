namespace ShopKeeper.Api.Tests.Employees;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Auth.Commands;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Application.Employees.Commands;
using ShopKeeper.Application.Employees.Queries;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Enums;
using ShopKeeper.Infrastructure.Identity;

public class EmployeesTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));
    private readonly TestEmailSender _emailSender = new();

    [Fact]
    public async Task InviteEmployee_CreatesPendingInvitation_AndSendsEmail()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var cashierRole = await context.Roles.SingleAsync(r => r.Name == DefaultRoles.Cashier);

        await new InviteEmployeeCommandHandler(context, owner, _emailSender).Handle(
            new InviteEmployeeCommand("newhire@shop.test", cashierRole.Id, seeded.BranchId), CancellationToken.None);

        var invitation = await context.PendingInvitations.SingleAsync();
        Assert.Equal("newhire@shop.test", invitation.Email);
        Assert.Null(invitation.AcceptedAt);
        Assert.True(invitation.ExpiresAt > DateTimeOffset.UtcNow.AddDays(6));

        Assert.NotNull(_emailSender.LastInvite);
        Assert.Equal("newhire@shop.test", _emailSender.LastInvite!.Value.ToEmail);
        Assert.Equal(invitation.Token, _emailSender.LastInvite!.Value.InviteToken);
    }

    [Fact]
    public async Task InviteEmployee_AlreadyPending_ThrowsConflict()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var cashierRole = await context.Roles.SingleAsync(r => r.Name == DefaultRoles.Cashier);

        await new InviteEmployeeCommandHandler(context, owner, _emailSender).Handle(
            new InviteEmployeeCommand("dupe@shop.test", cashierRole.Id, null), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => new InviteEmployeeCommandHandler(context, owner, _emailSender).Handle(
            new InviteEmployeeCommand("dupe@shop.test", cashierRole.Id, null), CancellationToken.None));
    }

    [Fact]
    public async Task InviteEmployee_WithoutEmployeesManagePermission_ThrowsForbidden()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var context = _db.CreateContext(seeded.AsOwner());
        var cashierRole = await context.Roles.SingleAsync(r => r.Name == DefaultRoles.Cashier);

        var cashier = new TestCurrentUserService
        {
            UserId = Guid.NewGuid(),
            BusinessId = seeded.BusinessId,
            BranchId = seeded.BranchId,
            IsOwner = false,
            PermissionsList = DefaultRoles.RolePermissionKeys[DefaultRoles.Cashier].ToList(),
        };

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => new InviteEmployeeCommandHandler(context, cashier, _emailSender).Handle(
            new InviteEmployeeCommand("someone@shop.test", cashierRole.Id, null), CancellationToken.None));
    }

    [Fact]
    public async Task AcceptInvitation_AsNewUser_CreatesUserAndBusinessUser_AndIssuesTokens()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var cashierRole = await context.Roles.SingleAsync(r => r.Name == DefaultRoles.Cashier);

        await new InviteEmployeeCommandHandler(context, owner, _emailSender).Handle(
            new InviteEmployeeCommand("newhire@shop.test", cashierRole.Id, seeded.BranchId), CancellationToken.None);
        var token = _emailSender.LastInvite!.Value.InviteToken;

        var tokenIssuer = new TokenIssuer(context, _jwt);
        var result = await new AcceptInvitationCommandHandler(context, _hasher, tokenIssuer).Handle(
            new AcceptInvitationCommand(token, "Passw0rd!", "Kofi", "Mensah", null), CancellationToken.None);

        Assert.Equal("newhire@shop.test", result.User.Email);
        Assert.Single(result.User.Businesses);
        Assert.Equal(seeded.BusinessId, result.User.Businesses[0].BusinessId);
        Assert.NotEmpty(result.AccessToken);

        var membership = await context.BusinessUsers.IgnoreQueryFilters()
            .SingleAsync(bu => bu.BusinessId == seeded.BusinessId && bu.User.Email == "newhire@shop.test");
        Assert.Equal(BusinessUserStatus.Active, membership.Status);
        Assert.Equal(seeded.BranchId, membership.BranchId);

        var invitation = await context.PendingInvitations.SingleAsync();
        Assert.NotNull(invitation.AcceptedAt);
    }

    [Fact]
    public async Task AcceptInvitation_Expired_ThrowsConflict()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var cashierRole = await context.Roles.SingleAsync(r => r.Name == DefaultRoles.Cashier);

        await new InviteEmployeeCommandHandler(context, owner, _emailSender).Handle(
            new InviteEmployeeCommand("expired@shop.test", cashierRole.Id, null), CancellationToken.None);
        var invitation = await context.PendingInvitations.SingleAsync();
        invitation.ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1);
        await context.SaveChangesAsync(CancellationToken.None);

        var tokenIssuer = new TokenIssuer(context, _jwt);
        await Assert.ThrowsAsync<ConflictException>(() => new AcceptInvitationCommandHandler(context, _hasher, tokenIssuer).Handle(
            new AcceptInvitationCommand(invitation.Token, "Passw0rd!", "Kofi", "Mensah", null), CancellationToken.None));
    }

    [Fact]
    public async Task AcceptInvitation_AsExistingUser_AttachesWithoutReRegistering()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var tokenIssuer = new TokenIssuer(context, _jwt);
        var cashierRole = await context.Roles.SingleAsync(r => r.Name == DefaultRoles.Cashier);

        // Register the invitee as a standalone user (e.g. they already use ShopKeeper elsewhere) first.
        var registerResult = await new RegisterCommandHandler(context, _hasher, tokenIssuer).Handle(
            new RegisterCommand("existing@shop.test", "Passw0rd!", "Ama", "Boateng", null), CancellationToken.None);

        await new InviteEmployeeCommandHandler(context, owner, _emailSender).Handle(
            new InviteEmployeeCommand("existing@shop.test", cashierRole.Id, seeded.BranchId), CancellationToken.None);
        var token = _emailSender.LastInvite!.Value.InviteToken;

        var invitee = new TestCurrentUserService { UserId = registerResult.User.Id, IsOwner = false };
        var inviteeContext = _db.CreateContext(invitee);

        var result = await new AcceptInvitationForExistingUserCommandHandler(inviteeContext, invitee, tokenIssuer).Handle(
            new AcceptInvitationForExistingUserCommand(token, null), CancellationToken.None);

        Assert.Equal(seeded.BusinessId, result.User.Businesses.Single(b => b.BusinessId == seeded.BusinessId).BusinessId);

        var userCount = await context.Users.CountAsync(u => u.Email == "existing@shop.test");
        Assert.Equal(1, userCount); // no duplicate user created
    }

    [Fact]
    public async Task RemoveEmployee_LastOwner_ThrowsConflict()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var ownerMembership = await context.BusinessUsers.SingleAsync(bu => bu.UserId == seeded.OwnerId);

        await Assert.ThrowsAsync<ConflictException>(() => new RemoveEmployeeCommandHandler(context, owner).Handle(
            new RemoveEmployeeCommand(ownerMembership.Id), CancellationToken.None));
    }

    [Fact]
    public async Task RemoveEmployee_RegularMember_SetsStatusRemoved()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var tokenIssuer = new TokenIssuer(context, _jwt);
        var cashierRole = await context.Roles.SingleAsync(r => r.Name == DefaultRoles.Cashier);

        await new InviteEmployeeCommandHandler(context, owner, _emailSender).Handle(
            new InviteEmployeeCommand("newhire@shop.test", cashierRole.Id, null), CancellationToken.None);
        var token = _emailSender.LastInvite!.Value.InviteToken;
        await new AcceptInvitationCommandHandler(context, _hasher, tokenIssuer).Handle(
            new AcceptInvitationCommand(token, "Passw0rd!", "Kofi", "Mensah", null), CancellationToken.None);

        var membership = await context.BusinessUsers.IgnoreQueryFilters()
            .SingleAsync(bu => bu.BusinessId == seeded.BusinessId && bu.User.Email == "newhire@shop.test");

        await new RemoveEmployeeCommandHandler(context, owner).Handle(new RemoveEmployeeCommand(membership.Id), CancellationToken.None);

        var updated = await context.BusinessUsers.IgnoreQueryFilters().SingleAsync(bu => bu.Id == membership.Id);
        Assert.Equal(BusinessUserStatus.Removed, updated.Status);
    }

    [Fact]
    public async Task InviteEmployee_AsAdministrator_ToOwnerRole_ThrowsForbidden()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var context = _db.CreateContext(seeded.AsOwner());
        var ownerRole = await context.Roles.SingleAsync(r => r.Name == DefaultRoles.Owner);

        var administrator = new TestCurrentUserService
        {
            UserId = Guid.NewGuid(),
            BusinessId = seeded.BusinessId,
            IsOwner = false,
            PermissionsList = DefaultRoles.RolePermissionKeys[DefaultRoles.Administrator].ToList(),
        };

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => new InviteEmployeeCommandHandler(context, administrator, _emailSender).Handle(
            new InviteEmployeeCommand("wannabe-owner@shop.test", ownerRole.Id, null), CancellationToken.None));
    }

    [Fact]
    public async Task InviteEmployee_AsAdministrator_ToNonOwnerRole_Succeeds()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var tokenIssuer = new TokenIssuer(context, _jwt);
        var cashierRole = await context.Roles.SingleAsync(r => r.Name == DefaultRoles.Cashier);

        // A real registered user, standing in for a genuine Administrator (InviteEmployeeCommand
        // looks up the inviter's name from db.Users, so the persona needs to actually exist).
        var registerResult = await new RegisterCommandHandler(context, _hasher, tokenIssuer).Handle(
            new RegisterCommand("admin@shop.test", "Passw0rd!", "Adjoa", "Owusu", null), CancellationToken.None);
        var administrator = new TestCurrentUserService
        {
            UserId = registerResult.User.Id,
            BusinessId = seeded.BusinessId,
            IsOwner = false,
            PermissionsList = DefaultRoles.RolePermissionKeys[DefaultRoles.Administrator].ToList(),
        };

        // A non-owner managing a non-Owner role is normal, unaffected employee management.
        await new InviteEmployeeCommandHandler(context, administrator, _emailSender).Handle(
            new InviteEmployeeCommand("newhire@shop.test", cashierRole.Id, null), CancellationToken.None);

        Assert.NotNull(_emailSender.LastInvite);
    }

    [Fact]
    public async Task InviteEmployee_AsOwner_ToOwnerRole_Succeeds()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var ownerRole = await context.Roles.SingleAsync(r => r.Name == DefaultRoles.Owner);

        await new InviteEmployeeCommandHandler(context, owner, _emailSender).Handle(
            new InviteEmployeeCommand("co-owner@shop.test", ownerRole.Id, null), CancellationToken.None);

        Assert.NotNull(_emailSender.LastInvite);
    }

    [Fact]
    public async Task AcceptInvitation_OwnerRoleInvite_SetsIsOwnerTrue()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var ownerRole = await context.Roles.SingleAsync(r => r.Name == DefaultRoles.Owner);

        await new InviteEmployeeCommandHandler(context, owner, _emailSender).Handle(
            new InviteEmployeeCommand("co-owner@shop.test", ownerRole.Id, null), CancellationToken.None);
        var token = _emailSender.LastInvite!.Value.InviteToken;

        var tokenIssuer = new TokenIssuer(context, _jwt);
        await new AcceptInvitationCommandHandler(context, _hasher, tokenIssuer).Handle(
            new AcceptInvitationCommand(token, "Passw0rd!", "Kwame", "Asante", null), CancellationToken.None);

        var membership = await context.BusinessUsers.IgnoreQueryFilters()
            .SingleAsync(bu => bu.BusinessId == seeded.BusinessId && bu.User.Email == "co-owner@shop.test");
        Assert.True(membership.IsOwner);
    }

    [Fact]
    public async Task AcceptInvitationForExistingUser_OwnerRoleInvite_SetsIsOwnerTrue()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var tokenIssuer = new TokenIssuer(context, _jwt);
        var ownerRole = await context.Roles.SingleAsync(r => r.Name == DefaultRoles.Owner);

        var registerResult = await new RegisterCommandHandler(context, _hasher, tokenIssuer).Handle(
            new RegisterCommand("existing-coowner@shop.test", "Passw0rd!", "Yaw", "Darko", null), CancellationToken.None);

        await new InviteEmployeeCommandHandler(context, owner, _emailSender).Handle(
            new InviteEmployeeCommand("existing-coowner@shop.test", ownerRole.Id, null), CancellationToken.None);
        var token = _emailSender.LastInvite!.Value.InviteToken;

        var invitee = new TestCurrentUserService { UserId = registerResult.User.Id, IsOwner = false };
        var inviteeContext = _db.CreateContext(invitee);

        await new AcceptInvitationForExistingUserCommandHandler(inviteeContext, invitee, tokenIssuer).Handle(
            new AcceptInvitationForExistingUserCommand(token, null), CancellationToken.None);

        var membership = await context.BusinessUsers.IgnoreQueryFilters()
            .SingleAsync(bu => bu.BusinessId == seeded.BusinessId && bu.User.Email == "existing-coowner@shop.test");
        Assert.True(membership.IsOwner);
    }

    [Fact]
    public async Task RemoveEmployee_AsNonOwner_RemovingAnOwner_ThrowsForbidden()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var ownerMembership = await context.BusinessUsers.SingleAsync(bu => bu.UserId == seeded.OwnerId);

        var administrator = new TestCurrentUserService
        {
            UserId = Guid.NewGuid(),
            BusinessId = seeded.BusinessId,
            IsOwner = false,
            PermissionsList = DefaultRoles.RolePermissionKeys[DefaultRoles.Administrator].ToList(),
        };

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => new RemoveEmployeeCommandHandler(context, administrator).Handle(
            new RemoveEmployeeCommand(ownerMembership.Id), CancellationToken.None));

        var stillActive = await context.BusinessUsers.SingleAsync(bu => bu.Id == ownerMembership.Id);
        Assert.Equal(BusinessUserStatus.Active, stillActive.Status);
    }

    [Fact]
    public async Task RemoveEmployee_AsOwner_RemovingAnotherOwner_WhenThirdOwnerExists_Succeeds()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var tokenIssuer = new TokenIssuer(context, _jwt);
        var ownerRole = await context.Roles.SingleAsync(r => r.Name == DefaultRoles.Owner);

        // Bring in a second co-owner so removing one still leaves an active owner behind.
        await new InviteEmployeeCommandHandler(context, owner, _emailSender).Handle(
            new InviteEmployeeCommand("co-owner@shop.test", ownerRole.Id, null), CancellationToken.None);
        var token = _emailSender.LastInvite!.Value.InviteToken;
        await new AcceptInvitationCommandHandler(context, _hasher, tokenIssuer).Handle(
            new AcceptInvitationCommand(token, "Passw0rd!", "Kwame", "Asante", null), CancellationToken.None);

        var coOwnerMembership = await context.BusinessUsers.IgnoreQueryFilters()
            .SingleAsync(bu => bu.BusinessId == seeded.BusinessId && bu.User.Email == "co-owner@shop.test");

        await new RemoveEmployeeCommandHandler(context, owner).Handle(new RemoveEmployeeCommand(coOwnerMembership.Id), CancellationToken.None);

        var updated = await context.BusinessUsers.IgnoreQueryFilters().SingleAsync(bu => bu.Id == coOwnerMembership.Id);
        Assert.Equal(BusinessUserStatus.Removed, updated.Status);
    }

    [Fact]
    public async Task RemoveEmployee_TargetingAnotherBusinessesOwner_ThrowsNotFound()
    {
        var businessA = await PosTestFixture.SeedAsync(_db, _hasher, _jwt, "ownerA@shop.test");
        var businessB = await PosTestFixture.SeedAsync(_db, _hasher, _jwt, "ownerB@shop.test");
        var ownerBContext = _db.CreateContext(businessB.AsOwner());
        var ownerBMembership = await ownerBContext.BusinessUsers.SingleAsync(bu => bu.UserId == businessB.OwnerId);

        // Business A's owner (full authority in their own tenant) must not be able to even see,
        // let alone remove, Business B's owner - the new IsOwner check must not short-circuit
        // ahead of the existing tenant query filter.
        var contextA = _db.CreateContext(businessA.AsOwner());
        await Assert.ThrowsAsync<NotFoundException>(() => new RemoveEmployeeCommandHandler(contextA, businessA.AsOwner()).Handle(
            new RemoveEmployeeCommand(ownerBMembership.Id), CancellationToken.None));

        var stillActive = await ownerBContext.BusinessUsers.SingleAsync(bu => bu.Id == ownerBMembership.Id);
        Assert.Equal(BusinessUserStatus.Active, stillActive.Status);
    }

    [Fact]
    public async Task GetBusinessUsers_ReturnsMembersAndPendingInvitations()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var cashierRole = await context.Roles.SingleAsync(r => r.Name == DefaultRoles.Cashier);

        await new InviteEmployeeCommandHandler(context, owner, _emailSender).Handle(
            new InviteEmployeeCommand("pending@shop.test", cashierRole.Id, null), CancellationToken.None);

        var result = await new GetBusinessUsersQueryHandler(context, owner).Handle(new GetBusinessUsersQuery(), CancellationToken.None);

        Assert.Single(result.Members); // just the seeded owner
        Assert.True(result.Members[0].IsOwner);
        Assert.Single(result.PendingInvitations);
        Assert.Equal("pending@shop.test", result.PendingInvitations[0].Email);
    }

    public void Dispose() => _db.Dispose();
}
