namespace ShopKeeper.Application.Uploads.Commands;

using FluentValidation;
using MediatR;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;

public record UploadProfilePhotoCommand(Stream Content, string ContentType) : IRequest<string>;

public class UploadProfilePhotoCommandValidator : AbstractValidator<UploadProfilePhotoCommand>
{
    private static readonly HashSet<string> AllowedContentTypes =
        new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp", "image/gif" };

    public UploadProfilePhotoCommandValidator()
    {
        RuleFor(x => x.ContentType)
            .Must(ct => AllowedContentTypes.Contains(ct))
            .WithMessage("Only JPEG, PNG, WEBP, or GIF images are allowed.");
    }
}

/// <summary>No PermissionKeys check, unlike UploadProductImageCommand - uploading your own profile
/// photo isn't gated by a business role, only by being an authenticated user at all.</summary>
public class UploadProfilePhotoCommandHandler(
    IFileStorageService storage, IImageProcessor imageProcessor, ICurrentUserService currentUser)
    : IRequestHandler<UploadProfilePhotoCommand, string>
{
    public async Task<string> Handle(UploadProfilePhotoCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequireUserId();

        // See UploadProductImageCommandHandler's comment - the Content-Type check above is
        // just a fast-path, IImageProcessor is the real security boundary.
        var processed = await imageProcessor.ProcessAsync(request.Content, cancellationToken);
        var fileName = $"{Guid.NewGuid()}.png";
        return await storage.SaveAsync(processed, fileName, "profile-photos", cancellationToken);
    }
}
