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

public class UploadProductImageCommandHandler(
    IFileStorageService storage, IImageProcessor imageProcessor, ICurrentUserService currentUser)
    : IRequestHandler<UploadProductImageCommand, string>
{
    public async Task<string> Handle(UploadProductImageCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.ProductsManage);

        // The Content-Type check above is a cheap fast-path rejection, not the real security
        // boundary - it's client-declared and trivially spoofable. IImageProcessor is what
        // actually verifies this is a genuine image (decodes it with a real codec) and
        // produces the bytes that get stored, always a freshly re-encoded PNG regardless of
        // what was uploaded - see its doc comment.
        var processed = await imageProcessor.ProcessAsync(request.Content, cancellationToken);
        var fileName = $"{Guid.NewGuid()}.png";
        return await storage.SaveAsync(processed, fileName, "products", cancellationToken);
    }
}
