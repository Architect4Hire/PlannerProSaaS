using Microsoft.EntityFrameworkCore;
using PlannerPro.Access.Core.Managers.Models.Domain;
using PlannerPro.Access.Tests.TestSupport;

namespace PlannerPro.Access.Tests.Tenancy;

/// <summary>
/// The read side of tenant isolation: a context scoped to one tenant cannot see another tenant's rows
/// via the automatic query filter — for the two entities most likely to be queried by id from a
/// tenant-scoped controller once one exists (<see cref="TenantMembership"/>, <see cref="Invitation"/>).
/// </summary>
public sealed class TenantQueryFilterAccessTests : IDisposable
{
    private readonly SqliteAccessDbContextFactory _factory = new();

    [Fact]
    public async Task TenantMembership_FromAnotherTenant_IsInvisible()
    {
        var tenantB = Guid.NewGuid();
        var tenantA = Guid.NewGuid();
        var membershipId = Guid.NewGuid();

        using (var seedContext = _factory.CreateContext(StaticTenantContext.For(tenantB)))
        {
            await SeedTenantAsync(seedContext, tenantB);
            seedContext.TenantMemberships.Add(new TenantMembership
            {
                Id = membershipId, UserId = Guid.NewGuid(), Role = TenantRole.Member, DefaultCapacityPoints = 5,
            });
            await seedContext.SaveChangesAsync();
        }

        using var contextA = _factory.CreateContext(StaticTenantContext.For(tenantA));
        var visibleToA = await contextA.TenantMemberships.FirstOrDefaultAsync(m => m.Id == membershipId);

        Assert.Null(visibleToA);
    }

    [Fact]
    public async Task Invitation_FromAnotherTenant_IsInvisible()
    {
        var tenantB = Guid.NewGuid();
        var tenantA = Guid.NewGuid();
        var invitationId = Guid.NewGuid();

        using (var seedContext = _factory.CreateContext(StaticTenantContext.For(tenantB)))
        {
            await SeedTenantAsync(seedContext, tenantB);
            seedContext.Invitations.Add(new Invitation
            {
                Id = invitationId, Email = "person@example.com", Token = "tok-2", ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            });
            await seedContext.SaveChangesAsync();
        }

        using var contextA = _factory.CreateContext(StaticTenantContext.For(tenantA));
        var visibleToA = await contextA.Invitations.FirstOrDefaultAsync(i => i.Id == invitationId);

        Assert.Null(visibleToA);
    }

    /// <summary>Seeds the Tenant row TenantMembership/Invitation's FK requires, on the same context and
    /// tenant scope the caller will add the dependent row under next.</summary>
    private static Task SeedTenantAsync(Core.Data.AccessDbContext context, Guid tenantId)
    {
        context.Tenants.Add(new Tenant { Id = tenantId, Slug = tenantId.ToString(), Name = tenantId.ToString() });
        return context.SaveChangesAsync();
    }

    public void Dispose() => _factory.Dispose();
}
