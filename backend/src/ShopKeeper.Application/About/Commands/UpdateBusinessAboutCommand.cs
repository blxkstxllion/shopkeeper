namespace ShopKeeper.Application.About.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;

public record UpdateBusinessAboutCommand(string? Description, string? OwnerBio) : IRequest;

public class UpdateBusinessAboutCommandValidator : AbstractValidator<UpdateBusinessAboutCommand>
{
    public UpdateBusinessAboutCommandValidator()
    {
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.OwnerBio).MaximumLength(2000);
    }
}

public class UpdateBusinessAboutCommandHandler(IAppDbContext db, ICurrentUserService currentUser) : IRequestHandler<UpdateBusinessAboutCommand>
{
    public async Task Handle(UpdateBusinessAboutCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.SettingsManage);
        var businessId = currentUser.RequireBusinessId();

        var business = await db.Businesses.FirstOrDefaultAsync(b => b.Id == businessId, cancellationToken)
            ?? throw new NotFoundException(nameof(Business), businessId);

        business.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        business.OwnerBio = string.IsNullOrWhiteSpace(request.OwnerBio) ? null : request.OwnerBio.Trim();

        await db.SaveChangesAsync(cancellationToken);
    }
}
