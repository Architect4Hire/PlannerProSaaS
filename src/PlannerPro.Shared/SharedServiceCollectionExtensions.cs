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
    /// Registers <see cref="ITenantContext"/> (scoped, populated per request by
    /// <see cref="TenantContextMiddleware"/>) and <see cref="TenantSaveChangesInterceptor"/>.
    /// This does NOT register the middleware itself — call
    /// <c>app.UseMiddleware&lt;TenantContextMiddleware&gt;()</c> early in each host's pipeline — and
    /// does NOT wire the interceptor into any <see cref="Microsoft.EntityFrameworkCore.DbContextOptions"/>;
    /// EF Core does not auto-discover DI-registered interceptors, so each service's own DbContext
    /// registration must do it explicitly:
    /// <code>
    /// services.AddDbContext&lt;PlanningDbContext&gt;((sp, options) =&gt;
    ///     options.UseSqlServer(...).AddInterceptors(sp.GetRequiredService&lt;TenantSaveChangesInterceptor&gt;()));
    /// </code>
    /// Use <c>AddDbContext</c>, not <c>AddDbContextPool</c> — the tenant query filter depends on a
    /// fresh context instance per DI scope; see the remarks on <see
    /// cref="Persistence.SharedDbContext.OnModelCreating"/>.
    /// </summary>
    public static IServiceCollection AddSharedTenancy(this IServiceCollection services)
    {
        services.TryAddScoped<TenantContext>();
        services.TryAddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.TryAddScoped<TenantSaveChangesInterceptor>();
        return services;
    }
}
