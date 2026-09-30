namespace ShopKeeper.Api.Tests.Uploads;

using FluentValidation;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Uploads.Commands;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Infrastructure.Storage;
using SkiaSharp;

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

    // The real processor, not a fake - the whole point of this component is to genuinely
    // decode/validate/re-encode image bytes (see IImageProcessor's doc comment on why a
    // Content-Type header check alone isn't a real security boundary), so a mock here would
    // defeat the actual thing under test. Matches this repo's real-SQLite-over-mocks philosophy.
    private static readonly SkiaImageProcessor ImageProcessor = new();

    // A real, minimal 1x1 transparent GIF89a - SkiaSharp can decode GIF but its encoder only
    // supports PNG/JPEG/WEBP, so unlike the other formats this can't be generated via
    // SKImage.Encode. Well-known bytes (the classic 1x1 tracking-pixel GIF).
    private static readonly byte[] GifBytes = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAUwAOw==");

    /// <summary>A genuinely valid, tiny (2x2) image encoded in the given format - not a magic
    /// byte array, generated with the same library the processor uses to decode it, so this
    /// stays correct if the format's actual encoding ever changed.</summary>
    private static Stream ValidImageBytes(SKEncodedImageFormat format)
    {
        if (format == SKEncodedImageFormat.Gif)
        {
            return new MemoryStream(GifBytes);
        }

        using var bitmap = new SKBitmap(2, 2);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(format, 100);
        return new MemoryStream(encoded.ToArray());
    }

    private static Stream GarbageBytes() => new MemoryStream([1, 2, 3, 4]);

    [Theory]
    [InlineData("image/jpeg", SKEncodedImageFormat.Jpeg)]
    [InlineData("image/png", SKEncodedImageFormat.Png)]
    [InlineData("image/webp", SKEncodedImageFormat.Webp)]
    [InlineData("image/gif", SKEncodedImageFormat.Gif)]
    public async Task Handle_WithGenuineImageOfEachAllowedType_SavesAsReEncodedPng(string contentType, SKEncodedImageFormat sourceFormat)
    {
        var storage = new FakeFileStorageService();
        var currentUser = new TestCurrentUserService { IsOwner = true };
        var handler = new UploadProductImageCommandHandler(storage, ImageProcessor, currentUser);

        var url = await handler.Handle(
            new UploadProductImageCommand(ValidImageBytes(sourceFormat), contentType), CancellationToken.None);

        Assert.Equal("products", storage.LastFolder);
        // Always .png regardless of the source format - see SkiaImageProcessor's doc comment.
        // What's stored is always freshly re-encoded bytes this processor itself generated,
        // never a pass-through of whatever the client actually uploaded.
        Assert.Equal(".png", storage.LastFileNameExtension);
        Assert.StartsWith("/uploads/products/", url);
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("text/plain")]
    [InlineData("application/x-msdownload")]
    public async Task Validator_RejectsNonImageContentTypes(string contentType)
    {
        var result = await Validator.ValidateAsync(new UploadProductImageCommand(GarbageBytes(), contentType));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Handle_WithNonImageBytesButSpoofedImageContentType_ThrowsValidation()
    {
        // This is the actual regression test for the security fix: the Content-Type header is
        // entirely client-declared and trivially spoofable (a renamed executable can claim to
        // be "image/png"), so the validator's header check alone would let this straight
        // through. It's IImageProcessor - which decodes the real bytes with a real codec -
        // that has to catch it instead.
        var storage = new FakeFileStorageService();
        var currentUser = new TestCurrentUserService { IsOwner = true };
        var handler = new UploadProductImageCommandHandler(storage, ImageProcessor, currentUser);

        await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(new UploadProductImageCommand(GarbageBytes(), "image/png"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithOversizedImageDimensions_ThrowsValidation()
    {
        var storage = new FakeFileStorageService();
        var currentUser = new TestCurrentUserService { IsOwner = true };
        var handler = new UploadProductImageCommandHandler(storage, ImageProcessor, currentUser);

        using var bitmap = new SKBitmap(6001, 10);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        var oversized = new MemoryStream(encoded.ToArray());

        await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(new UploadProductImageCommand(oversized, "image/png"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithoutProductsManagePermission_ThrowsForbidden()
    {
        var storage = new FakeFileStorageService();
        var currentUser = new TestCurrentUserService { IsOwner = false, PermissionsList = [] };
        var handler = new UploadProductImageCommandHandler(storage, ImageProcessor, currentUser);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            handler.Handle(new UploadProductImageCommand(ValidImageBytes(SKEncodedImageFormat.Png), "image/png"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithProductsManagePermission_ButNotOwner_Succeeds()
    {
        var storage = new FakeFileStorageService();
        var currentUser = new TestCurrentUserService { IsOwner = false, PermissionsList = [PermissionKeys.ProductsManage] };
        var handler = new UploadProductImageCommandHandler(storage, ImageProcessor, currentUser);

        var url = await handler.Handle(
            new UploadProductImageCommand(ValidImageBytes(SKEncodedImageFormat.Png), "image/png"), CancellationToken.None);

        Assert.StartsWith("/uploads/products/", url);
    }
}
