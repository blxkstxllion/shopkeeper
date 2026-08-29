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

public class UploadBusinessLogoCommandHandler(IFileStorageService storage, ICurrentUserService currentUser)
    : IRequestHandler<UploadBusinessLogoCommand, string>
{
    public async Task<string> Handle(UploadBusinessLogoCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.SettingsManage);

        var extension = request.ContentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => throw new InvalidOperationException("Unreachable - content type already validated."),
        };

        var fileName = $"{Guid.NewGuid()}{extension}";
        return await storage.SaveAsync(request.Content, fileName, "business-logos", cancellationToken);
    }
}
