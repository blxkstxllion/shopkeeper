namespace ShopKeeper.Api.Tests.Auth;

using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Auth.Commands;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Infrastructure.Identity;

public class UpdateProfilePhotoCommandTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));

    [Fact]
    public async Task Handle_WithValidUploadedUrl_SetsPhotoUrl()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        await new UpdateProfilePhotoCommandHandler(context).Handle(
            new UpdateProfilePhotoCommand(owner.UserId!.Value, "/uploads/profile-photos/abc123.jpg"), CancellationToken.None);

        var user = await context.Users.SingleAsync(u => u.Id == owner.UserId);
        Assert.Equal("/uploads/profile-photos/abc123.jpg", user.PhotoUrl);
    }

    [Fact]
    public async Task Handle_ForUnknownUser_ThrowsNotFound()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateProfilePhotoCommandHandler(context).Handle(
                new UpdateProfilePhotoCommand(Guid.NewGuid(), "/uploads/profile-photos/abc123.jpg"), CancellationToken.None));
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://evil.example.com/tracker.png")]
    [InlineData("/uploads/products/abc123.jpg")] // a real upload path, just the wrong folder
    public async Task Validator_RejectsAnythingNotFromTheProfilePhotoUploadEndpoint(string photoUrl)
    {
        var validator = new UpdateProfilePhotoCommandValidator();

        var result = await validator.ValidateAsync(new UpdateProfilePhotoCommand(Guid.NewGuid(), photoUrl));

        Assert.False(result.IsValid);
    }

    public void Dispose() => _db.Dispose();
}
