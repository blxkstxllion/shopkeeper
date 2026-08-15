namespace ShopKeeper.Application.Common.Behaviors;

using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Entities;

/// <summary>
/// Writes one AuditLog row for every successfully-handled *Command (never Queries - reads
/// aren't audited). Runs after the command's own handler and SaveChangesAsync have already
/// committed, as a separate save, so a failure here can never roll back or block the real
/// operation - audit logging is deliberately best-effort and swallow-on-error.
/// </summary>
public class AuditLoggingBehavior<TRequest, TResponse>(
    IAppDbContext db, ICurrentUserService currentUser, ILogger<AuditLoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly HashSet<string> RedactedPropertyNameFragments = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "token", "secret", "hash",
    };

    // Verb prefixes stripped from a command's type name to guess a human-readable EntityType,
    // e.g. "RemoveEmployeeCommand" -> "Employee". Best-effort only - EntityType is nullable and
    // purely a filtering convenience for the audit log UI, never load-bearing.
    private static readonly string[] VerbPrefixes =
    [
        "Create", "Update", "Delete", "Remove", "Approve", "Reject", "Submit", "Revoke", "Regenerate",
        "Adjust", "Restock", "Refund", "Void", "Switch", "Change", "Reset", "Verify", "Enable", "Disable",
        "Register", "Login", "Logout", "Accept", "Invite", "Complete", "Generate", "Upload",
    ];

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next();

        var requestTypeName = typeof(TRequest).Name;
        if (!requestTypeName.EndsWith("Command", StringComparison.Ordinal))
        {
            return response;
        }

        try
        {
            var action = requestTypeName[..^"Command".Length];
            var entityType = action;
            foreach (var verb in VerbPrefixes)
            {
                if (entityType.StartsWith(verb, StringComparison.Ordinal))
                {
                    entityType = entityType[verb.Length..];
                    break;
                }
            }

            db.AuditLogs.Add(new AuditLog
            {
                BusinessId = currentUser.BusinessId,
                UserId = currentUser.UserId,
                Action = action,
                EntityType = string.IsNullOrEmpty(entityType) ? null : entityType,
                EntityId = FindLikelyEntityId(request),
                NewValue = SerializeRedacted(request),
                IpAddress = currentUser.IpAddress,
                UserAgent = currentUser.UserAgent,
            });

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Never let an audit-log write failure surface as if the actual command failed -
            // the command already committed successfully above.
            logger.LogError(ex, "Failed to write audit log for {RequestType}", requestTypeName);
        }

        return response;
    }

    private static Guid? FindLikelyEntityId(TRequest request)
    {
        var idProperty = typeof(TRequest).GetProperties().FirstOrDefault(p => p.Name == "Id" && p.PropertyType == typeof(Guid))
            ?? typeof(TRequest).GetProperties().FirstOrDefault(p =>
                p.Name.EndsWith("Id", StringComparison.Ordinal) && p.PropertyType == typeof(Guid));

        return idProperty?.GetValue(request) as Guid?;
    }

    private static string? SerializeRedacted(TRequest request)
    {
        var redacted = new Dictionary<string, object?>();
        foreach (var property in typeof(TRequest).GetProperties())
        {
            redacted[property.Name] = RedactedPropertyNameFragments.Any(f => property.Name.Contains(f, StringComparison.OrdinalIgnoreCase))
                ? "[REDACTED]"
                : property.GetValue(request);
        }

        return JsonSerializer.Serialize(redacted);
    }
}
