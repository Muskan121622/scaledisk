using ScaleDesk.Domain.Common;
using ScaleDesk.Domain.Enums;

namespace ScaleDesk.Domain.Events;

public sealed record CustomerCreated(
    Guid TenantId, Guid CustomerId) : IDomainEvent
{
    public string EventType => "customer.created";
}

public sealed record TeamCreated(
    Guid TenantId, Guid TeamId, string Name) : IDomainEvent
{
    public string EventType => "team.created";
}

public sealed record MemberInvited(
    Guid TenantId, Guid MembershipId, string Email, UserRole Role) : IDomainEvent
{
    public string EventType => "member.invited";
}

public sealed record EmailVerificationRequested(
    Guid TenantId, Guid UserId, string Email) : IDomainEvent
{
    public string EventType => "user.email_verification_requested";
}
