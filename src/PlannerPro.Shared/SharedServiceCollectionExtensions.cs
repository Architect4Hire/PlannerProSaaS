using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PlannerPro.Shared.Caching;
using PlannerPro.Shared.Exceptions;
using PlannerPro.Shared.Persistence;
using PlannerPro.Shared.Tenancy;

namespace PlannerPro.Shared;

public static class SharedServiceCollectionExtensions
{
    /// <summary>
    /// Registers the outbox, inbox, and cache abstraction for <typeparamref name="TContext"/>.
    /// <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/> is registered as an
    /// in-memory cache by default (<c>AddDistributedMemoryCache</c> uses try-add semantics); a
    /// service that wires a different backend (e.g. Redis via the Aspire client integration) must
    /// register it BEFORE calling this method so its registration wins.
    /// <see cref="Inbox{TContext}"/> depends on <see cref="Tenancy.ITenantContext"/> (it sources the
    /// tenant id it stamps from there, never from a caller), so <see cref="AddSharedTenancy"/> must
    /// also be called on the same <see cref="IServiceCollection"/>.
    /// </summary>
    public static IServiceCollection AddSharedPersistence<TContext>(this IServiceCollection services)
        where TContext : SharedDbContext
    {
        services.AddDistributedMemoryCache();
        services.TryAddScoped<ICacheService, DistributedCacheService>();
        services.TryAddScoped<IOutbox, Outbox<TContext>>();
        services.TryAddScoped<IInbox, Inbox<TContext>>();
        return services;
    }

    public static IServiceCollection AddSharedExceptionHandler(this IServiceCollection services)
    {
        services.AddExceptionHandler<SharedExceptionHandler>();
        services.AddProblemDetails();
        return services;
    }

    /// <summary>
    /// Registers <see cref="ITenantContext"/>, scoped, populated per request by
    /// <see cref="TenantContextMiddleware"/>. This does NOT register the middleware itself — call
    /// <c>app.UseMiddleware&lt;TenantContextMiddleware&gt;()</c> early in each host's pipeline, and
    /// BEFORE <c>app.UseAuthorization()</c> if any authorization policy will ever read
    /// <see cref="ITenantContext"/> (a role-based policy resolves during <c>UseAuthorization</c>, so
    /// tenant headers must already be parsed by then).
    /// </summary>
    /// <remarks>
    /// Does NOT register <see cref="TenantSaveChangesInterceptor"/> — <see
    /// cref="Persistence.SharedDbContext"/> wires it itself, per-instance, from its own already-injected
    /// <see cref="ITenantContext"/> (see the remarks on <see cref="Persistence.SharedDbContext.OnConfiguring"/>).
    /// This is what lets every service register its DbContext with nothing more than:
    /// <code>
    /// builder.Services.AddSharedTenancy();
    /// builder.Services.AddDbContext&lt;PlanningDbContext&gt;(options =&gt;
    ///     options.UseSqlServer(builder.Configuration.GetConnectionString("planningdb")));
    /// </code>
    /// The connection string is still Aspire-injected via <c>WithReference</c> on the AppHost side, not
    /// hardcoded — reading it from configuration is not the same thing as inventing it.
    /// <b>Deliberately plain <c>AddDbContext</c>, not the <c>Aspire.Microsoft.EntityFrameworkCore.SqlServer</c>
    /// package's <c>AddSqlServerDbContext&lt;TContext&gt;(...)</c>:</b> that integration pools DbContext
    /// instances unconditionally with no supported opt-out (confirmed against <c>dotnet/aspire</c>
    /// issue #7023, closed "not planned"), and pooling is flatly incompatible with this mechanism — a
    /// pooled context is built once against the ROOT provider and reused across scopes, so it can never
    /// see the per-request scoped <see cref="ITenantContext"/> the query filter and interceptor both
    /// depend on. Never <c>AddDbContextPool</c> or any pooled variant, Aspire's or otherwise; see the
    /// remarks on <see cref="Persistence.SharedDbContext.OnModelCreating"/>.
    /// </remarks>
    public static IServiceCollection AddSharedTenancy(this IServiceCollection services)
    {
        services.TryAddScoped<TenantContext>();
        services.TryAddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        return services;
    }
}
