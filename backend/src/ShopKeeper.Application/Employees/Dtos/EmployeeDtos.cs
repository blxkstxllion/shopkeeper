namespace ShopKeeper.Application.Employees.Dtos;

public record RoleDto(Guid Id, string Name);

public record BusinessMemberDto(
    Guid BusinessUserId,
    Guid UserId,
    string FirstName,
    string LastName,
    string Email,
    string RoleName,
    string? BranchName,
    string Status,
    bool IsOwner,
    DateTimeOffset? JoinedAt);

public record PendingInvitationDto(
    Guid Id,
    string Email,
    string RoleName,
    string? BranchName,
    DateTimeOffset InvitedAt,
    DateTimeOffset ExpiresAt);

public record BusinessUsersDto(
    IReadOnlyList<BusinessMemberDto> Members,
    IReadOnlyList<PendingInvitationDto> PendingInvitations);

/// <summary>UserAlreadyExists tells the accept-invite page whether to render a set-password
/// form (new user) or a "log in, then accept" prompt (existing user) - see AcceptInvitationCommand
/// vs AcceptInvitationForExistingUserCommand.</summary>
public record InvitationDetailsDto(
    Guid BusinessId,
    string Email,
    string BusinessName,
    string InviterName,
    string RoleName,
    bool UserAlreadyExists);
