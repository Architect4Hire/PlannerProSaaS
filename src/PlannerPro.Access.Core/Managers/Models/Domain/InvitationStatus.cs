namespace PlannerPro.Access.Core.Managers.Models.Domain;

/// <summary>The lifecycle of one <see cref="Invitation"/>.</summary>
public enum InvitationStatus
{
    Pending,
    Accepted,
    Revoked,
    Expired,
}
