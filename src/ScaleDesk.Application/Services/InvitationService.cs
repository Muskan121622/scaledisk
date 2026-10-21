using ScaleDesk.Application.Abstractions;
using ScaleDesk.Application.Abstractions.Repositories;
using ScaleDesk.Application.Common;
using ScaleDesk.Application.Common.Security;
using ScaleDesk.Application.Contracts.Invitations;
using ScaleDesk.Application.Exceptions;
using ScaleDesk.Application.Options;
using ScaleDesk.Domain.Constants;
using ScaleDesk.Domain.Entities;
using ScaleDesk.Domain.Enums;
using ScaleDesk.Domain.Events;

namespace ScaleDesk.Application.Services;

/// <summary>
/// Workspace invitations + the email-verification-through-acceptance flow. Inviting is a privileged,
/// tenant-bound action; accepting is anonymous and driven entirely by the out-of-band token, which
/// is why possession of the target mailbox is what confirms the email address.
/// </summary>
public sealed class InvitationService : IInvitationService
{
    private readonly IInvitationRepository _invitations;
    private readonly IUserRepository _users;
    private readonly IMembershipRepository _memberships;
    private readonly ITenantRepository _tenants;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly IEmailSender _email;
    private readonly IEventCollector _events;
    private readonly AuthOptions _options;

    public InvitationService(
        IInvitationRepository invitations,
        IUserRepository users,
        IMembershipRepository memberships,
        ITenantRepository tenants,
        IPasswordHasher passwordHasher,
        ICurrentTenant tenant,
        ICurrentUser user,
        IUnitOfWork uow,
        IClock clock,
        IEmailSender email,
        IEventCollector events,
        AuthOptions options)
    {
        _invitations = invitations;
        _users = users;
        _memberships = memberships;
        _tenants = tenants;
        _passwordHasher = passwordHasher;
        _tenant = tenant;
        _user = user;
        _uow = uow;
        _clock = clock;
        _email = email;
        _events = events;
        _options = options;
    }

    public async Task<Result<InvitationResponse>> InviteAsync(CreateInvitationRequest request, CancellationToken ct = default)
    {
        RequirePermission(Permissions.MembersInvite);
        var tenantId = _tenant.RequireTenant();
        var email = NormalizeEmail(request.Email);

        if (request.Role == UserRole.Owner)
            throw new ValidationException("role", "Ownership cannot be invited; assign an Admin instead.");

        // Don't invite someone who already belongs to this workspace.
        var existingUser = await _users.GetByEmailAsync(email, ct);
        if (existingUser is not null &&
            await _memberships.GetAsync(existingUser.Id, tenantId, ct) is { Status: MembershipStatus.Active })
            throw new ConflictException($"{email} is already a member of this workspace.");

        var token = OpaqueToken.Generate();
        var now = _clock.UtcNow;
        var expiresAt = now.AddDays(_options.InvitationExpirationDays);

        // Re-inviting rotates the token on the existing pending row rather than piling up invites.
        var invitation = await _invitations.FindPendingByEmailAsync(email, ct);
        if (invitation is null)
        {
            invitation = new Invitation
            {
                TenantId = tenantId,
                Email = email,
                CreatedAt = now
            };
            await _invitations.AddAsync(invitation, ct);
        }

        invitation.Role = request.Role;
        invitation.TokenHash = OpaqueToken.Hash(token);
        invitation.Status = InvitationStatus.Pending;
        invitation.InvitedByUserId = _user.UserId;
        invitation.ExpiresAt = expiresAt;
        invitation.AcceptedAt = null;

        await _uow.SaveChangesAsync(ct);

        // Best-effort notification: a mail-provider hiccup must not undo the persisted invitation.
        await TrySendInviteEmail(email, token, ct);

        return Result.Ok(Map(invitation));
    }

    public async Task<IReadOnlyList<InvitationResponse>> ListPendingAsync(CancellationToken ct = default)
    {
        RequirePermission(Permissions.MembersInvite);
        var invitations = await _invitations.ListPendingAsync(ct);
        return invitations.Select(Map).ToList();
    }

    public async Task<Result> RevokeAsync(Guid id, CancellationToken ct = default)
    {
        RequirePermission(Permissions.MembersInvite);
        var invitation = await _invitations.GetAsync(id, ct)
            ?? throw new NotFoundException("Invitation not found in this workspace.");

        if (invitation.Status == InvitationStatus.Pending)
        {
            invitation.Status = InvitationStatus.Revoked;
            _invitations.Update(invitation);
            await _uow.SaveChangesAsync(ct);
        }

        return Result.Ok();
    }

    public async Task<Result<AcceptInvitationResponse>> AcceptAsync(AcceptInvitationRequest request, CancellationToken ct = default)
    {
        // Anonymous: resolved purely by the token hash, across tenant filters.
        var invitation = await _invitations.FindByTokenHashAsync(OpaqueToken.Hash(request.Token), ct);
        if (invitation is null || !invitation.IsUsable(_clock.UtcNow))
            return Result.Fail<AcceptInvitationResponse>("invalid_invitation", "This invitation is invalid or has expired.");

        var tenant = await _tenants.GetByIdAsync(invitation.TenantId, ct)
            ?? throw new NotFoundException("The workspace for this invitation no longer exists.");

        var now = _clock.UtcNow;
        var user = await _users.GetByEmailAsync(invitation.Email, ct);

        if (user is null)
        {
            EnsureProfile(request.FirstName, request.LastName, request.Password);
            user = new User
            {
                Email = invitation.Email,
                PasswordHash = _passwordHasher.Hash(request.Password),
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                EmailConfirmed = true, // clicking the emailed link proves mailbox control
                CreatedAt = now
            };
            await _users.AddAsync(user, ct);
        }
        else
        {
            // Existing account accepting an invite is likewise proof they control the address.
            user.EmailConfirmed = true;
        }

        var existingMembership = await _memberships.GetAsync(user.Id, invitation.TenantId, ct);
        if (existingMembership is { Status: MembershipStatus.Active })
            throw new ConflictException($"You are already a member of {tenant.Name}.");

        var membership = existingMembership ?? new Membership
        {
            TenantId = invitation.TenantId,
            UserId = user.Id,
            CreatedAt = now
        };
        membership.Role = invitation.Role;
        membership.Status = MembershipStatus.Active;
        membership.InvitedByUserId = invitation.InvitedByUserId;
        await _memberships.SaveAsync(membership, ct);

        invitation.Status = InvitationStatus.Accepted;
        invitation.AcceptedAt = now;
        _invitations.Update(invitation);

        _events.Raise(new MemberInvited(invitation.TenantId, membership.Id, invitation.Email, invitation.Role));
        await _uow.SaveChangesAsync(ct);

        return Result.Ok(new AcceptInvitationResponse(user.Email, tenant.Name, membership.Role));
    }

    private async Task TrySendInviteEmail(string to, string token, CancellationToken ct)
    {
        try
        {
            var link = $"/accept-invite?token={Uri.EscapeDataString(token)}";
            var body = $"<p>You've been invited to ScaleDesk.</p><p>Accept: <a href=\"{link}\">{link}</a></p>";
            await _email.SendAsync(to, "You've been invited to join ScaleDesk", body, ct);
        }
        catch (Exception)
        {
            // Swallowed deliberately: the invitation is already committed; delivery is retried by ops.
        }
    }

    private void RequirePermission(string permission)
    {
        if (!_user.HasPermission(permission))
            throw new ForbiddenException($"Missing required permission '{permission}'.");
    }

    private static void EnsureProfile(string firstName, string lastName, string password)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(firstName)) errors["firstName"] = ["First name is required."];
        if (string.IsNullOrWhiteSpace(lastName)) errors["lastName"] = ["Last name is required."];
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            errors["password"] = ["Password must be at least 8 characters."];
        if (errors.Count > 0)
            throw new ValidationException(errors);
    }

    private static string NormalizeEmail(string email)
    {
        var trimmed = (email ?? string.Empty).Trim().ToLowerInvariant();
        if (trimmed.Length == 0 || !trimmed.Contains('@'))
            throw new ValidationException("email", "A valid email address is required.");
        return trimmed;
    }

    private static InvitationResponse Map(Invitation i) => new(
        i.Id, i.Email, i.Role, i.Status, i.ExpiresAt, i.CreatedAt);
}
