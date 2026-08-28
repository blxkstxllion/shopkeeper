namespace ShopKeeper.Application.Uploads.Commands;

using FluentValidation;
using MediatR;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;

public record UploadProductImageCommand(Stream Content, string ContentType) : IRequest<string>;

public class UploadProductImageCommandValidator : AbstractValidator<UploadProductImageCommand>
{
    private static readonly HashSet<string> AllowedContentTypes =
        new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp", "image/gif" };

    public UploadProductImageCommandValidator()
    {
        RuleFor(x => x.ContentType)
            .Must(ct => AllowedContentTypes.Contains(ct))
            .WithMessage("Only JPEG, PNG, WEBP, or GIF images are allowed.");
    }
}

public class UploadProductImageCommandHandler(IFileStorageService storage, ICurrentUserService currentUser)
    : IRequestHandler<UploadProductImageCommand, string>
{
    public async Task<string> Handle(UploadProductImageCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.ProductsManage);

        var extension = request.ContentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => throw new InvalidOperationException("Unreachable - content type already validated."),
        };

        var fileName = $"{Guid.NewGuid()}{extension}";
        return await storage.SaveAsync(request.Content, fileName, "products", cancellationToken);
    }
}
