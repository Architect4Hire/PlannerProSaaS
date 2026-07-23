using Microsoft.EntityFrameworkCore;
using PlannerPro.Access.Core.Managers.Models.Domain;
using PlannerPro.Access.Tests.TestSupport;
using PlannerPro.Shared.Exceptions;

namespace PlannerPro.Access.Tests.Tenancy;

/// <summary>
/// Confirms the generic <c>TenantSaveChangesInterceptor</c> behavior (already covered generically in
/// <c>PlannerPro.Shared.Tests</c>) holds for Access's specific entities — most importantly
/// <see cref="Tenant"/>, the one entity where <c>TenantId</c> is self-referential rather than a
/// reference to something else.
/// </summary>
public sealed class TenantSaveChangesInterceptorAccessTests : IDisposable
{
    private readonly SqliteAccessDbContextFactory _factory = new();

    [Fact]
    public async Task AddTenant_UnderBypass_WithTenantIdExplicitlySetEqualToItsOwnId_Persists()
    {
        using var context = _factory.CreateContext(StaticTenantContext.Bypass);
        var id = Guid.NewGuid();

        // Creating a brand-new tenant is unavoidably a system operation: under a normal resolved
        // context, ApplyToAdded always stamps the AMBIENT tenant id, so a caller already scoped into
        // tenant A can never mint a new, different tenant row — only the bypass path can.
        context.Tenants.Add(new Tenant { Id = id, TenantId = id, Slug = "acme", Name = "Acme" });
        await context.SaveChangesAsync();

        Assert.Equal(id, (await context.Tenants.FirstAsync(t => t.Id == id)).TenantId);
    }

    [Fact]
    public async Task AddTenant_UnderBypass_WithoutExplicitTenantId_Throws()
    {
        using var context = _factory.CreateContext(StaticTenantContext.Bypass);

        context.Tenants.Add(new Tenant { Id = Guid.NewGuid(), Slug = "orphan", Name = "Orphan" });

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task ModifyTenantMembership_FromAnotherTenant_ThrowsCrossTenantWriteException()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var membershipId = Guid.NewGuid();

        using (var seedContext = _factory.CreateContext(StaticTenantContext.For(tenantB)))
        {
            seedContext.Tenants.Add(new Tenant { Id = tenantB, Slug = "tenant-b", Name = "Tenant B" });
            await seedContext.SaveChangesAsync();

            seedContext.TenantMemberships.Add(new TenantMembership
            {
                Id = membershipId, UserId = Guid.NewGuid(), Role = TenantRole.Member, DefaultCapacityPoints = 5,
            });
            await seedContext.SaveChangesAsync();
        }

        using var contextA = _factory.CreateContext(StaticTenantContext.For(tenantA));
        var tracked = new TenantMembership
        {
            Id = membershipId, TenantId = tenantB, UserId = Guid.NewGuid(), Role = TenantRole.Admin, DefaultCapacityPoints = 5,
        };
        contextA.Attach(tracked);
        tracked.Role = TenantRole.Owner;
        contextA.Entry(tracked).State = EntityState.Modified;

        await Assert.ThrowsAsync<CrossTenantWriteException>(() => contextA.SaveChangesAsync());
    }

    [Fact]
    public async Task ModifyInvitation_FromAnotherTenant_ThrowsCrossTenantWriteException()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var invitationId = Guid.NewGuid();

        using (var seedContext = _factory.CreateContext(StaticTenantContext.For(tenantB)))
        {
            seedContext.Tenants.Add(new Tenant { Id = tenantB, Slug = "tenant-b-2", Name = "Tenant B" });
            await seedContext.SaveChangesAsync();

            seedContext.Invitations.Add(new Invitation
            {
                Id = invitationId, Email = "person@example.com", Token = "tok-1", ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            });
            await seedContext.SaveChangesAsync();
        }

        using var contextA = _factory.CreateContext(StaticTenantContext.For(tenantA));
        var tracked = new Invitation
        {
            Id = invitationId, TenantId = tenantB, Email = "person@example.com", Token = "tok-1", ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
        };
        contextA.Attach(tracked);
        contextA.Entry(tracked).State = EntityState.Deleted;

        await Assert.ThrowsAsync<CrossTenantWriteException>(() => contextA.SaveChangesAsync());
    }

    public void Dispose() => _factory.Dispose();
}
