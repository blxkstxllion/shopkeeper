namespace ShopKeeper.Application.LocalEdition.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.LocalEdition.Dtos;

/// <summary>Offline-edition-only. Polled by the frontend on launch to decide between the setup
/// wizard and the PIN lock screen - deliberately unauthenticated (see LocalController), since
/// there's no identity to authenticate as before setup has even run.</summary>
public record GetLocalStatusQuery : IRequest<LocalStatusDto>;

public class GetLocalStatusQueryHandler(IAppDbContext db) : IRequestHandler<GetLocalStatusQuery, LocalStatusDto>
{
    public async Task<LocalStatusDto> Handle(GetLocalStatusQuery request, CancellationToken cancellationToken)
    {
        var settings = await db.LocalAppSettings.FirstOrDefaultAsync(cancellationToken);
        return new LocalStatusDto(settings is not null, settings?.StoreName);
    }
}
