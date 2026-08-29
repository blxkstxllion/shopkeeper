namespace ShopKeeper.Api.Tests.Notifications;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Application.Employees.Commands;
using ShopKeeper.Application.Inventory.Commands;
using ShopKeeper.Application.Notifications.Commands;
using ShopKeeper.Application.Notifications.Queries;
using ShopKeeper.Application.Products.Commands;
using ShopKeeper.Application.Sales.Commands;
using ShopKeeper.Domain.Enums;
using ShopKeeper.Infrastructure.Identity;

public class NotificationsTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));

    [Fact]
    public async Task SubmitJoinRequest_NotifiesOwner()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var code = await new RegenerateJoinCodeCommandHandler(context, owner).Handle(new RegenerateJoinCodeCommand(), CancellationToken.None);

        await new SubmitJoinRequestCommandHandler(context, _hasher, new NotificationDispatcher(context)).Handle(
            new SubmitJoinRequestCommand(code, "Kofi", "Mensah", "notify-me@shop.test", "0244000000", "Passw0rd!"),
            CancellationToken.None);

        var notification = await context.Notifications.SingleAsync(n => n.UserId == seeded.OwnerId);
        Assert.Equal("JoinRequestSubmitted", notification.Type);
        Assert.Contains("Kofi Mensah", notification.Message);
        Assert.Equal("/app/employees", notification.Link);
        Assert.Null(notification.ReadAt);
    }

    [Fact]
    public async Task AdjustStock_CrossingMinimumStock_NotifiesOwnerOnce()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var notifications = new NotificationDispatcher(context);

        var product = await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Widget", "SKU-LOWSTOCK", null, null, null, null, 10m, 6m, 5, true, 10, seeded.BranchId),
            CancellationToken.None);

        // 10 -> 6: still above the reorder level of 5, no alert yet.
        await new AdjustStockCommandHandler(context, owner, notifications).Handle(
            new AdjustStockCommand(product.Id, seeded.BranchId, -4, "Sold"), CancellationToken.None);
        Assert.Empty(await context.Notifications.Where(n => n.Type == "LowStock").ToListAsync());

        // 6 -> 4: crosses the reorder level of 5 - alert fires.
        await new AdjustStockCommandHandler(context, owner, notifications).Handle(
            new AdjustStockCommand(product.Id, seeded.BranchId, -2, "Sold"), CancellationToken.None);
        Assert.Single(await context.Notifications.Where(n => n.Type == "LowStock").ToListAsync());

        // 4 -> 2: still below the reorder level - must not fire again.
        await new AdjustStockCommandHandler(context, owner, notifications).Handle(
            new AdjustStockCommand(product.Id, seeded.BranchId, -2, "Sold"), CancellationToken.None);
        Assert.Single(await context.Notifications.Where(n => n.Type == "LowStock").ToListAsync());
    }

    [Fact]
    public async Task CreateSale_CrossingMinimumStock_NotifiesOwner()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var product = await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Widget", "SKU-SALE-LOWSTOCK", null, null, null, null, 10m, 6m, 5, true, 6, seeded.BranchId),
            CancellationToken.None);

        await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(product.Id, 2, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 20m, null)]),
            CancellationToken.None);

        var notification = await context.Notifications.SingleAsync(n => n.Type == "LowStock");
        Assert.Contains("Widget", notification.Message);
        Assert.Equal("/app/inventory", notification.Link);
    }

    [Fact]
    public async Task GetNotificationPreferences_NoRowYet_DefaultsToAllEnabled()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var preferences = await new GetNotificationPreferencesQueryHandler(context, owner).Handle(
            new GetNotificationPreferencesQuery(), CancellationToken.None);

        Assert.True(preferences.NotifyOnJoinRequest);
        Assert.True(preferences.NotifyOnLowStock);
    }

    [Fact]
    public async Task UpdateNotificationPreferences_CreatesRowThenUpdatesItOnSubsequentCalls()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        await new UpdateNotificationPreferencesCommandHandler(context, owner).Handle(
            new UpdateNotificationPreferencesCommand(false, true), CancellationToken.None);

        Assert.Single(await context.NotificationPreferences.ToListAsync());
        var afterFirstUpdate = await new GetNotificationPreferencesQueryHandler(context, owner).Handle(
            new GetNotificationPreferencesQuery(), CancellationToken.None);
        Assert.False(afterFirstUpdate.NotifyOnJoinRequest);
        Assert.True(afterFirstUpdate.NotifyOnLowStock);

        // A second update must edit the same row, not add a duplicate.
        await new UpdateNotificationPreferencesCommandHandler(context, owner).Handle(
            new UpdateNotificationPreferencesCommand(false, false), CancellationToken.None);

        Assert.Single(await context.NotificationPreferences.ToListAsync());
        var afterSecondUpdate = await new GetNotificationPreferencesQueryHandler(context, owner).Handle(
            new GetNotificationPreferencesQuery(), CancellationToken.None);
        Assert.False(afterSecondUpdate.NotifyOnJoinRequest);
        Assert.False(afterSecondUpdate.NotifyOnLowStock);
    }

    [Fact]
    public async Task SubmitJoinRequest_WhenOwnerMutedJoinRequests_DoesNotNotify()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        await new UpdateNotificationPreferencesCommandHandler(context, owner).Handle(
            new UpdateNotificationPreferencesCommand(false, true), CancellationToken.None);

        var code = await new RegenerateJoinCodeCommandHandler(context, owner).Handle(new RegenerateJoinCodeCommand(), CancellationToken.None);
        await new SubmitJoinRequestCommandHandler(context, _hasher, new NotificationDispatcher(context)).Handle(
            new SubmitJoinRequestCommand(code, "Kofi", "Mensah", "muted-join@shop.test", "0244000000", "Passw0rd!"),
            CancellationToken.None);

        Assert.Empty(await context.Notifications.Where(n => n.Type == "JoinRequestSubmitted").ToListAsync());
    }

    [Fact]
    public async Task AdjustStock_WhenOwnerMutedLowStock_DoesNotNotify_ButOtherTypesStillFire()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var notifications = new NotificationDispatcher(context);

        // Muting only LowStock must not affect JoinRequestSubmitted - the mute is per-type.
        await new UpdateNotificationPreferencesCommandHandler(context, owner).Handle(
            new UpdateNotificationPreferencesCommand(true, false), CancellationToken.None);

        var product = await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Widget", "SKU-MUTED-LOWSTOCK", null, null, null, null, 10m, 6m, 5, true, 6, seeded.BranchId),
            CancellationToken.None);

        await new AdjustStockCommandHandler(context, owner, notifications).Handle(
            new AdjustStockCommand(product.Id, seeded.BranchId, -2, "Sold"), CancellationToken.None);
        Assert.Empty(await context.Notifications.Where(n => n.Type == "LowStock").ToListAsync());

        var code = await new RegenerateJoinCodeCommandHandler(context, owner).Handle(new RegenerateJoinCodeCommand(), CancellationToken.None);
        await new SubmitJoinRequestCommandHandler(context, _hasher, notifications).Handle(
            new SubmitJoinRequestCommand(code, "Ama", "Boateng", "still-notified@shop.test", "0244000001", "Passw0rd!"),
            CancellationToken.None);
        Assert.Single(await context.Notifications.Where(n => n.Type == "JoinRequestSubmitted").ToListAsync());
    }

    [Fact]
    public async Task GetNotifications_ReturnsOnlyCurrentUsersOwnRows()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var otherUserId = Guid.NewGuid();

        context.BusinessUsers.Add(new ShopKeeper.Domain.Entities.BusinessUser
        {
            BusinessId = seeded.BusinessId,
            UserId = otherUserId,
            User = new ShopKeeper.Domain.Entities.User { Id = otherUserId, Email = "other@shop.test", PasswordHash = "x", FirstName = "Kwame", LastName = "Asante" },
            RoleId = (await context.Roles.SingleAsync(r => r.Name == ShopKeeper.Domain.Constants.DefaultRoles.Cashier)).Id,
            IsOwner = false,
            Status = BusinessUserStatus.Active,
            JoinedAt = DateTimeOffset.UtcNow,
        });
        context.Notifications.Add(new ShopKeeper.Domain.Entities.Notification
        {
            BusinessId = seeded.BusinessId,
            UserId = seeded.OwnerId,
            Type = "Test",
            Title = "Mine",
            Message = "Mine",
        });
        context.Notifications.Add(new ShopKeeper.Domain.Entities.Notification
        {
            BusinessId = seeded.BusinessId,
            UserId = otherUserId,
            Type = "Test",
            Title = "Not mine",
            Message = "Not mine",
        });
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await new GetNotificationsQueryHandler(context, owner).Handle(new GetNotificationsQuery(1, 30), CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("Mine", result.Items[0].Title);
    }

    [Fact]
    public async Task GetUnreadNotificationCount_CountsOnlyUnread()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        context.Notifications.Add(new ShopKeeper.Domain.Entities.Notification
        {
            BusinessId = seeded.BusinessId,
            UserId = seeded.OwnerId,
            Type = "Test",
            Title = "Unread",
            Message = "Unread",
        });
        context.Notifications.Add(new ShopKeeper.Domain.Entities.Notification
        {
            BusinessId = seeded.BusinessId,
            UserId = seeded.OwnerId,
            Type = "Test",
            Title = "Read",
            Message = "Read",
            ReadAt = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync(CancellationToken.None);

        var count = await new GetUnreadNotificationCountQueryHandler(context, owner).Handle(new GetUnreadNotificationCountQuery(), CancellationToken.None);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task MarkNotificationRead_SetsReadAt()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var notification = new ShopKeeper.Domain.Entities.Notification
        {
            BusinessId = seeded.BusinessId,
            UserId = seeded.OwnerId,
            Type = "Test",
            Title = "T",
            Message = "M",
        };
        context.Notifications.Add(notification);
        await context.SaveChangesAsync(CancellationToken.None);

        await new MarkNotificationReadCommandHandler(context, owner).Handle(new MarkNotificationReadCommand(notification.Id), CancellationToken.None);

        var updated = await context.Notifications.SingleAsync(n => n.Id == notification.Id);
        Assert.NotNull(updated.ReadAt);
    }

    [Fact]
    public async Task MarkNotificationRead_BelongingToAnotherUser_ThrowsNotFound()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var otherUser = new ShopKeeper.Domain.Entities.User
        {
            Email = "other-notif@shop.test",
            PasswordHash = "x",
            FirstName = "Kwame",
            LastName = "Asante",
        };
        context.Users.Add(otherUser);

        var notification = new ShopKeeper.Domain.Entities.Notification
        {
            BusinessId = seeded.BusinessId,
            UserId = otherUser.Id,
            Type = "Test",
            Title = "T",
            Message = "M",
        };
        context.Notifications.Add(notification);
        await context.SaveChangesAsync(CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() => new MarkNotificationReadCommandHandler(context, owner).Handle(
            new MarkNotificationReadCommand(notification.Id), CancellationToken.None));
    }

    [Fact]
    public async Task MarkAllNotificationsRead_MarksAllOfCurrentUsersUnread()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        context.Notifications.Add(new ShopKeeper.Domain.Entities.Notification
        {
            BusinessId = seeded.BusinessId,
            UserId = seeded.OwnerId,
            Type = "Test",
            Title = "A",
            Message = "A",
        });
        context.Notifications.Add(new ShopKeeper.Domain.Entities.Notification
        {
            BusinessId = seeded.BusinessId,
            UserId = seeded.OwnerId,
            Type = "Test",
            Title = "B",
            Message = "B",
        });
        await context.SaveChangesAsync(CancellationToken.None);

        await new MarkAllNotificationsReadCommandHandler(context, owner).Handle(new MarkAllNotificationsReadCommand(), CancellationToken.None);

        var count = await new GetUnreadNotificationCountQueryHandler(context, owner).Handle(new GetUnreadNotificationCountQuery(), CancellationToken.None);
        Assert.Equal(0, count);
    }

    public void Dispose() => _db.Dispose();
}
