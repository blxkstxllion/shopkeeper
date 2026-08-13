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
