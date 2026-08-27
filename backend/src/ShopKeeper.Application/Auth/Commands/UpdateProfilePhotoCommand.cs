namespace ShopKeeper.Application.Auth.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Interfaces;

public record UpdateProfilePhotoCommand(Guid UserId, string PhotoUrl) : IRequest;

public class UpdateProfilePhotoCommandValidator : AbstractValidator<UpdateProfilePhotoCommand>
{
    public UpdateProfilePhotoCommandValidator()
    {
        // Requires the URL to have come from UploadProfilePhotoCommand first - without this, an
        // authenticated user (this command has no PermissionKeys gate, only "it's your own row")
        // could set their PhotoUrl to an arbitrary string later rendered in an <img src> app-wide.
        RuleFor(x => x.PhotoUrl).NotEmpty()
            .Must(url => url.StartsWith("/uploads/profile-photos/", StringComparison.Ordinal))
            .WithMessage("Photo must be uploaded via the upload endpoint first.");
    }
}

public class UpdateProfilePhotoCommandHandler(IAppDbContext db) : IRequestHandler<UpdateProfilePhotoCommand>
{
    public async Task Handle(UpdateProfilePhotoCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.User), request.UserId);

        user.PhotoUrl = request.PhotoUrl;
        await db.SaveChangesAsync(cancellationToken);
    }
}
