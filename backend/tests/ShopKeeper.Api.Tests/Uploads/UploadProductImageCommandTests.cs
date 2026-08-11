namespace ShopKeeper.Api.Tests.Uploads;

using FluentValidation;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Uploads.Commands;
using ShopKeeper.Domain.Constants;

/// <summary>Records what it was asked to save without touching disk - keeps this suite fast and hermetic.</summary>
public class FakeFileStorageService : IFileStorageService
{
    public string? LastFolder { get; private set; }
    public string? LastFileNameExtension { get; private set; }

    public Task<string> SaveAsync(Stream content, string fileName, string folder, CancellationToken ct = default)
    {
        LastFolder = folder;
        LastFileNameExtension = Path.GetExtension(fileName);
        return Task.FromResult($"/uploads/{folder}/{fileName}");
    }
}

public class UploadProductImageCommandTests
{
    private static readonly UploadProductImageCommandValidator Validator = new();

    private static Stream FakeImageBytes() => new MemoryStream([1, 2, 3, 4]);

    [Theory]
    [InlineData("image/jpeg", ".jpg")]
    [InlineData("image/png", ".png")]
    [InlineData("image/webp", ".webp")]
    [InlineData("image/gif", ".gif")]
    public async Task Handle_WithAllowedImageType_SavesUnderProductsFolderWithCorrectExtension(string contentType, string expectedExtension)
    {
        var storage = new FakeFileStorageService();
        var currentUser = new TestCurrentUserService { IsOwner = true };
        var handler = new UploadProductImageCommandHandler(storage, currentUser);

        var url = await handler.Handle(new UploadProductImageCommand(FakeImageBytes(), contentType), CancellationToken.None);

        Assert.Equal("products", storage.LastFolder);
        Assert.Equal(expectedExtension, storage.LastFileNameExtension);
        Assert.StartsWith("/uploads/products/", url);
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("text/plain")]
    [InlineData("application/x-msdownload")]
    public async Task Validator_RejectsNonImageContentTypes(string contentType)
    {
        var result = await Validator.ValidateAsync(new UploadProductImageCommand(FakeImageBytes(), contentType));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Handle_WithoutProductsManagePermission_ThrowsForbidden()
    {
        var storage = new FakeFileStorageService();
        var currentUser = new TestCurrentUserService { IsOwner = false, PermissionsList = [] };
        var handler = new UploadProductImageCommandHandler(storage, currentUser);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            handler.Handle(new UploadProductImageCommand(FakeImageBytes(), "image/png"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithProductsManagePermission_ButNotOwner_Succeeds()
    {
        var storage = new FakeFileStorageService();
        var currentUser = new TestCurrentUserService { IsOwner = false, PermissionsList = [PermissionKeys.ProductsManage] };
        var handler = new UploadProductImageCommandHandler(storage, currentUser);

        var url = await handler.Handle(new UploadProductImageCommand(FakeImageBytes(), "image/png"), CancellationToken.None);

        Assert.StartsWith("/uploads/products/", url);
    }
}
