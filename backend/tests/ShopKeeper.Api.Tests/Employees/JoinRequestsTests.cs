namespace ShopKeeper.Api.Tests.Employees;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Employees.Commands;
using ShopKeeper.Application.Employees.Queries;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Enums;
using ShopKeeper.Infrastructure.Identity;

public class JoinRequestsTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));

    [Fact]
    public async Task RegenerateJoinCode_SetsCode_AndGetJoinCodeReturnsIt()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var code = await new RegenerateJoinCodeCommandHandler(context, owner).Handle(new RegenerateJoinCodeCommand(), CancellationToken.None);

        Assert.Equal(8, code.Length);

        var fetched = await new GetJoinCodeQueryHandler(context, owner).Handle(new GetJoinCodeQuery(), CancellationToken.None);
        Assert.Equal(code, fetched);
    }

    [Fact]
    public async Task RevokeJoinCode_ClearsCode()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        await new RegenerateJoinCodeCommandHandler(context, owner).Handle(new RegenerateJoinCodeCommand(), CancellationToken.None);
        await new RevokeJoinCodeCommandHandler(context, owner).Handle(new RevokeJoinCodeCommand(), CancellationToken.None);

        var fetched = await new GetJoinCodeQueryHandler(context, owner).Handle(new GetJoinCodeQuery(), CancellationToken.None);
        Assert.Null(fetched);
    }

    [Fact]
    public async Task RegenerateJoinCode_WithoutEmployeesManagePermission_ThrowsForbidden()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var context = _db.CreateContext(seeded.AsOwner());

        var cashier = new TestCurrentUserService
        {
            UserId = Guid.NewGuid(),
            BusinessId = seeded.BusinessId,
            BranchId = seeded.BranchId,
            IsOwner = false,
            PermissionsList = DefaultRoles.RolePermissionKeys[DefaultRoles.Cashier].ToList(),
        };

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => new RegenerateJoinCodeCommandHandler(context, cashier).Handle(
            new RegenerateJoinCodeCommand(), CancellationToken.None));
    }

    [Fact]
    public async Task SubmitJoinRequest_CreatesDormantUserAndPendingRequest()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var code = await new RegenerateJoinCodeCommandHandler(context, owner).Handle(new RegenerateJoinCodeCommand(), CancellationToken.None);

        await new SubmitJoinRequestCommandHandler(context, _hasher).Handle(
            new SubmitJoinRequestCommand(code, "Kofi", "Mensah", "kofi@shop.test", "0244000000", "Passw0rd!"),
            CancellationToken.None);

        var user = await context.Users.SingleAsync(u => u.Email == "kofi@shop.test");
        Assert.False(user.IsEmailVerified);
        Assert.Equal("0244000000", user.PhoneNumber);

        var joinRequest = await context.JoinRequests.SingleAsync(r => r.UserId == user.Id);
        Assert.Equal(JoinRequestStatus.Pending, joinRequest.Status);
        Assert.Equal(seeded.BusinessId, joinRequest.BusinessId);

        Assert.Empty(context.BusinessUsers.Where(bu => bu.UserId == user.Id));
    }

    [Fact]
    public async Task SubmitJoinRequest_InvalidCode_ThrowsNotFound()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var context = _db.CreateContext(new TestCurrentUserService());

        await Assert.ThrowsAsync<NotFoundException>(() => new SubmitJoinRequestCommandHandler(context, _hasher).Handle(
            new SubmitJoinRequestCommand("BOGUSCOD", "Kofi", "Mensah", "kofi@shop.test", "0244000000", "Passw0rd!"),
            CancellationToken.None));
    }

    [Fact]
    public async Task SubmitJoinRequest_DuplicateEmail_ThrowsConflict()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var code = await new RegenerateJoinCodeCommandHandler(context, owner).Handle(new RegenerateJoinCodeCommand(), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => new SubmitJoinRequestCommandHandler(context, _hasher).Handle(
            new SubmitJoinRequestCommand(code, "Ama", "Owusu", "owner@shop.test", "0244000000", "Passw0rd!"),
            CancellationToken.None));
    }

    [Fact]
    public async Task SubmitJoinRequestForExistingUser_CreatesPendingRequest()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var code = await new RegenerateJoinCodeCommandHandler(context, owner).Handle(new RegenerateJoinCodeCommand(), CancellationToken.None);

        var otherBusiness = await PosTestFixture.SeedAsync(_db, _hasher, _jwt, "otherowner@shop.test");
        var otherOwner = otherBusiness.AsOwner();
        var otherContext = _db.CreateContext(otherOwner);

        await new SubmitJoinRequestForExistingUserCommandHandler(otherContext, otherOwner).Handle(
            new SubmitJoinRequestForExistingUserCommand(code), CancellationToken.None);

        var joinRequest = await context.JoinRequests.SingleAsync(r => r.UserId == otherBusiness.OwnerId);
        Assert.Equal(JoinRequestStatus.Pending, joinRequest.Status);
        Assert.Equal(seeded.BusinessId, joinRequest.BusinessId);
    }

    [Fact]
    public async Task SubmitJoinRequestForExistingUser_AlreadyMember_ThrowsConflict()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var code = await new RegenerateJoinCodeCommandHandler(context, owner).Handle(new RegenerateJoinCodeCommand(), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => new SubmitJoinRequestForExistingUserCommandHandler(context, owner).Handle(
            new SubmitJoinRequestForExistingUserCommand(code), CancellationToken.None));
    }

    [Fact]
    public async Task SubmitJoinRequestForExistingUser_DuplicatePending_ThrowsConflict()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var code = await new RegenerateJoinCodeCommandHandler(context, owner).Handle(new RegenerateJoinCodeCommand(), CancellationToken.None);

        var other = await PosTestFixture.SeedAsync(_db, _hasher, _jwt, "otherowner2@shop.test");
        var otherOwner = other.AsOwner();
        var otherContext = _db.CreateContext(otherOwner);

        await new SubmitJoinRequestForExistingUserCommandHandler(otherContext, otherOwner).Handle(
            new SubmitJoinRequestForExistingUserCommand(code), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => new SubmitJoinRequestForExistingUserCommandHandler(otherContext, otherOwner).Handle(
            new SubmitJoinRequestForExistingUserCommand(code), CancellationToken.None));
    }

    [Fact]
    public async Task ApproveJoinRequest_CreatesBusinessUser_AndMarksApproved()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var code = await new RegenerateJoinCodeCommandHandler(context, owner).Handle(new RegenerateJoinCodeCommand(), CancellationToken.None);

        await new SubmitJoinRequestCommandHandler(context, _hasher).Handle(
            new SubmitJoinRequestCommand(code, "Kofi", "Mensah", "kofi2@shop.test", "0244000001", "Passw0rd!"),
            CancellationToken.None);

        var user = await context.Users.SingleAsync(u => u.Email == "kofi2@shop.test");
        var joinRequest = await context.JoinRequests.SingleAsync(r => r.UserId == user.Id);
        var cashierRole = await context.Roles.SingleAsync(r => r.Name == DefaultRoles.Cashier);

        await new ApproveJoinRequestCommandHandler(context, owner).Handle(
            new ApproveJoinRequestCommand(joinRequest.Id, cashierRole.Id, seeded.BranchId), CancellationToken.None);

        var updated = await context.JoinRequests.SingleAsync(r => r.Id == joinRequest.Id);
        Assert.Equal(JoinRequestStatus.Approved, updated.Status);
        Assert.NotNull(updated.ReviewedAt);

        var membership = await context.BusinessUsers.SingleAsync(bu => bu.UserId == user.Id);
        Assert.Equal(Domain.Enums.BusinessUserStatus.Active, membership.Status);
        Assert.Equal(cashierRole.Id, membership.RoleId);
        Assert.Equal(seeded.BranchId, membership.BranchId);
    }

    [Fact]
    public async Task RejectJoinRequest_MarksRejected_NoBusinessUserCreated()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var code = await new RegenerateJoinCodeCommandHandler(context, owner).Handle(new RegenerateJoinCodeCommand(), CancellationToken.None);

        await new SubmitJoinRequestCommandHandler(context, _hasher).Handle(
            new SubmitJoinRequestCommand(code, "Kofi", "Mensah", "kofi3@shop.test", "0244000002", "Passw0rd!"),
            CancellationToken.None);

        var user = await context.Users.SingleAsync(u => u.Email == "kofi3@shop.test");
        var joinRequest = await context.JoinRequests.SingleAsync(r => r.UserId == user.Id);

        await new RejectJoinRequestCommandHandler(context, owner).Handle(new RejectJoinRequestCommand(joinRequest.Id), CancellationToken.None);

        var updated = await context.JoinRequests.SingleAsync(r => r.Id == joinRequest.Id);
        Assert.Equal(JoinRequestStatus.Rejected, updated.Status);
        Assert.Empty(context.BusinessUsers.Where(bu => bu.UserId == user.Id));
    }

    [Fact]
    public async Task ApproveJoinRequest_AlreadyReviewed_ThrowsConflict()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var code = await new RegenerateJoinCodeCommandHandler(context, owner).Handle(new RegenerateJoinCodeCommand(), CancellationToken.None);

        await new SubmitJoinRequestCommandHandler(context, _hasher).Handle(
            new SubmitJoinRequestCommand(code, "Kofi", "Mensah", "kofi4@shop.test", "0244000003", "Passw0rd!"),
            CancellationToken.None);

        var user = await context.Users.SingleAsync(u => u.Email == "kofi4@shop.test");
        var joinRequest = await context.JoinRequests.SingleAsync(r => r.UserId == user.Id);
        var cashierRole = await context.Roles.SingleAsync(r => r.Name == DefaultRoles.Cashier);

        await new RejectJoinRequestCommandHandler(context, owner).Handle(new RejectJoinRequestCommand(joinRequest.Id), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => new ApproveJoinRequestCommandHandler(context, owner).Handle(
            new ApproveJoinRequestCommand(joinRequest.Id, cashierRole.Id, null), CancellationToken.None));
    }

    [Fact]
    public async Task GetBusinessUsers_IncludesPendingJoinRequests()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var code = await new RegenerateJoinCodeCommandHandler(context, owner).Handle(new RegenerateJoinCodeCommand(), CancellationToken.None);

        await new SubmitJoinRequestCommandHandler(context, _hasher).Handle(
            new SubmitJoinRequestCommand(code, "Kofi", "Mensah", "kofi5@shop.test", "0244000004", "Passw0rd!"),
            CancellationToken.None);

        var result = await new GetBusinessUsersQueryHandler(context, owner).Handle(new GetBusinessUsersQuery(), CancellationToken.None);

        Assert.Single(result.JoinRequests);
        Assert.Equal("kofi5@shop.test", result.JoinRequests[0].Email);
        Assert.Equal("0244000004", result.JoinRequests[0].Phone);
    }

    public void Dispose() => _db.Dispose();
}
