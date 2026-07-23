using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PlannerPro.Shared.Caching;
using PlannerPro.Shared.Exceptions;
using PlannerPro.Shared.Persistence;

namespace PlannerPro.Shared;

public static class SharedServiceCollectionExtensions
{
    /// <summary>
    /// Registers the outbox, inbox, and cache abstraction for <typeparamref name="TContext"/>.
    /// <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/> is registered as an
    /// in-memory cache by default (<c>AddDistributedMemoryCache</c> uses try-add semantics); a
    /// service that wires a different backend (e.g. Redis via the Aspire client integration) must
    /// register it BEFORE calling this method so its registration wins.
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
}
