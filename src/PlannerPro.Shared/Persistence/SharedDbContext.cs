using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlannerPro.Shared.Tenancy;

namespace PlannerPro.Shared.Persistence;

public abstract class SharedDbContext(
    DbContextOptions options, ITenantContext tenant, ILogger<TenantSaveChangesInterceptor>? logger = null)
    : DbContext(options)
{
    /// <summary>
    /// The current tenant scope, read fresh at query-execution time by the filters this context
    /// applies below — see the "Filter references <c>Tenant</c> inline" note on <see cref="OnModelCreating"/>.
    /// </summary>
    protected ITenantContext Tenant { get; } = tenant;

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    /// <summary>
    /// Wires <see cref="TenantSaveChangesInterceptor"/> from THIS instance's own already-injected
    /// <see cref="Tenant"/> (and <paramref name="logger"/>, captured at construction), rather than
    /// resolving the interceptor from DI in each service's DbContext registration. Because a
    /// DbContext's own constructor parameters (beyond <see cref="DbContextOptions"/> itself) ARE
    /// resolved from DI when EF Core activates it, <see cref="Tenant"/> and <paramref name="logger"/>
    /// already arrive correctly scoped — this just reuses them instead of resolving the interceptor a
    /// second way. A derived context does not need to override this or add the interceptor itself; a
    /// service's own DbContext registration (<c>services.AddDbContext&lt;XDbContext&gt;(options =&gt;
    /// options.UseSqlServer(...))</c>) needs nothing beyond the connection.
    /// </summary>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(new TenantSaveChangesInterceptor(Tenant, logger));
        base.OnConfiguring(optionsBuilder);
    }

    /// <summary>
    /// Applies <see cref="OutboxMessageConfiguration"/>/<see cref="InboxMessageConfiguration"/> and,
    /// by reflecting over every entity type already in the model, a tenant query filter to every
    /// <see cref="ITenantScoped"/> entity. <c>OutboxMessage</c>/<c>InboxMessage</c> deliberately do
    /// NOT implement <see cref="ITenantScoped"/> — the outbox dispatcher and inbox check are system
    /// background processes that must see rows across every tenant.
    /// </summary>
    /// <remarks>
    /// EF Core builds and caches the model — including this filter's expression tree — exactly once
    /// per context CLR type. The filter below reads <c>Tenant.TenantId</c> / <c>Tenant.BypassFilters</c>
    /// (an instance member of <c>this</c>) INLINE in the lambda; EF Core rebinds any member-access
    /// chain rooted at <c>this</c> to whichever context instance is actually executing a given query,
    /// so the filter re-evaluates per request even though it was only ever compiled once. Do NOT hoist
    /// <c>Tenant</c> or any of its properties into a local variable before the lambda — that would
    /// capture a value baked in forever at whichever instant the model happened to be built, a silent
    /// and permanent cross-tenant leak.
    /// <para>
    /// A derived context that overrides <c>OnModelCreating</c> must call
    /// <c>base.OnModelCreating(modelBuilder)</c> LAST — any entity registered only via
    /// <c>modelBuilder.Entity&lt;T&gt;()</c> before that call won't yet be in the model when this
    /// reflection pass runs. (Not a concern for entities exposed via a <c>DbSet&lt;T&gt;</c> property,
    /// which EF discovers by convention before <c>OnModelCreating</c> runs at all.)
    /// </para>
    /// </remarks>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType)) continue;

            // EF Core only allows HasQueryFilter on the root of an inheritance hierarchy (it applies
            // automatically to derived types) — skip non-root types here; they're covered by the
            // assertion below, which fails loudly if their root turns out not to be ITenantScoped too.
            if (entityType.BaseType is not null) continue;

            SetTenantFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
        }

        AssertEveryTenantScopedTypeIsFiltered(modelBuilder);
    }

    private static readonly MethodInfo SetTenantFilterMethod =
        typeof(SharedDbContext).GetMethod(nameof(SetTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private void SetTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, ITenantScoped =>
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter("Tenant", (TEntity e) => e.TenantId == Tenant.TenantId || Tenant.BypassFilters);

    /// <summary>
    /// The loop above only filters the root of each inheritance hierarchy — EF Core applies that
    /// filter to every derived type automatically, PROVIDED the root itself implements
    /// <see cref="ITenantScoped"/>. If a future TPH hierarchy has an <see cref="ITenantScoped"/> type
    /// hanging off a root that does NOT implement it, the loop's root-only guard would otherwise skip
    /// it silently, leaving that type permanently unfiltered. Converts that silent gap into a startup
    /// failure instead — "an entity should not require remembering to add a filter; if it does, the
    /// mechanism is wrong" (tenancy.md) applies here just as much as to a missed filter outright.
    /// </summary>
    private static void AssertEveryTenantScopedTypeIsFiltered(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType)) continue;

            var root = entityType;
            while (root.BaseType is not null) root = root.BaseType;

            if (!typeof(ITenantScoped).IsAssignableFrom(root.ClrType))
            {
                throw new InvalidOperationException(
                    $"{entityType.ClrType.Name} implements ITenantScoped, but its inheritance root " +
                    $"{root.ClrType.Name} does not. EF Core can only apply a query filter to the root " +
                    "of an inheritance hierarchy, so this entity would go unfiltered. Make the root " +
                    "implement ITenantScoped too, or move TenantId off the derived type.");
            }
        }
    }
}
