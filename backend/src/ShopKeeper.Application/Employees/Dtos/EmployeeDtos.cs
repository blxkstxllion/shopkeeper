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

public record JoinRequestItemDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    DateTimeOffset RequestedAt);

public record BusinessUsersDto(
    IReadOnlyList<BusinessMemberDto> Members,
    IReadOnlyList<PendingInvitationDto> PendingInvitations,
    IReadOnlyList<JoinRequestItemDto> JoinRequests);

/// <summary>Returned by the unauthenticated join-code lookup - just enough to render the
/// "Join {BusinessName}" landing page before the visitor has an account or is logged in.</summary>
public record JoinBusinessDto(Guid BusinessId, string BusinessName);

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
