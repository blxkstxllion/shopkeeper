namespace ShopKeeper.Application.Uploads.Commands;

using FluentValidation;
using MediatR;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;

public record UploadBusinessLogoCommand(Stream Content, string ContentType) : IRequest<string>;

public class UploadBusinessLogoCommandValidator : AbstractValidator<UploadBusinessLogoCommand>
{
    private static readonly HashSet<string> AllowedContentTypes =
        new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp", "image/gif" };

    public UploadBusinessLogoCommandValidator()
    {
        RuleFor(x => x.ContentType)
            .Must(ct => AllowedContentTypes.Contains(ct))
            .WithMessage("Only JPEG, PNG, WEBP, or GIF images are allowed.");
    }
}

public class UploadBusinessLogoCommandHandler(
    IFileStorageService storage, IImageProcessor imageProcessor, ICurrentUserService currentUser)
    : IRequestHandler<UploadBusinessLogoCommand, string>
{
    public async Task<string> Handle(UploadBusinessLogoCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.SettingsManage);

        // See UploadProductImageCommandHandler's comment - the Content-Type check above is
        // just a fast-path, IImageProcessor is the real security boundary.
        await using var processed = await imageProcessor.ProcessAsync(request.Content, cancellationToken);
        var fileName = $"{Guid.NewGuid()}.png";
        return await storage.SaveAsync(processed, fileName, "business-logos", cancellationToken);
    }
}
