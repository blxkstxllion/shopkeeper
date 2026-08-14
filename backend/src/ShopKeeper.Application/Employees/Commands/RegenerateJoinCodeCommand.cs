namespace ShopKeeper.Application.Employees.Commands;

using System.Security.Cryptography;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;

/// <summary>Generates a fresh join code, overwriting (and immediately invalidating) any
/// previous one - no history is kept, this is a single mutable business setting, not a ledger.</summary>
public record RegenerateJoinCodeCommand : IRequest<string>;

public class RegenerateJoinCodeCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<RegenerateJoinCodeCommand, string>
{
    // Excludes visually-ambiguous characters (0/O, 1/I/L) so the code works equally well
    // scanned as a QR payload or typed in by hand as a PIN.
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 8;

    public async Task<string> Handle(RegenerateJoinCodeCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.EmployeesManage);
        var businessId = currentUser.RequireBusinessId();

        var setting = await db.BusinessSettings.FirstOrDefaultAsync(s => s.BusinessId == businessId, cancellationToken)
            ?? throw new NotFoundException(nameof(BusinessSetting), businessId);

        string code;
        do
        {
            code = GenerateCode();
        }
        while (await db.BusinessSettings.AnyAsync(s => s.JoinCode == code, cancellationToken));

        setting.JoinCode = code;
        await db.SaveChangesAsync(cancellationToken);

        return code;
    }

    private static string GenerateCode() =>
        string.Create(CodeLength, 0, (span, _) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            }
        });
}
