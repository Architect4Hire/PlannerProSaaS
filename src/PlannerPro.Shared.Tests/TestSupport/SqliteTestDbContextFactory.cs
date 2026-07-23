using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlannerPro.Shared.Persistence;
using PlannerPro.Shared.Tenancy;

namespace PlannerPro.Shared.Tests.TestSupport;

/// <summary>
/// EF Core's InMemory provider doesn't support BeginTransactionAsync/execution strategies at all,
/// so tests use a real (if lightweight) relational provider — Sqlite backed by an open in-memory
/// connection, which is destroyed once that connection closes.
/// </summary>
internal sealed class SqliteTestDbContextFactory : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public SqliteTestDbContextFactory()
    {
        _connection.Open();
    }

    public TestDbContext CreateContext(ITenantContext? tenant = null)
    {
        var tenantContext = tenant ?? StaticTenantContext.Bypass;

        // No .AddInterceptors(...) here — SharedDbContext.OnConfiguring wires
        // TenantSaveChangesInterceptor itself from the tenantContext passed to the constructor below.
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .Options;

        var context = new TestDbContext(options, tenantContext);
        context.Database.EnsureCreated();
        return context;
    }

    /// <summary>
    /// Adds the DI registrations a real host wires (<c>AddSharedTenancy</c> +
    /// <c>AddSharedPersistence</c>, minus the ASP.NET Core-only pieces) to <paramref name="services"/>,
    /// scoped to this factory's shared connection — for tests that, like
    /// <see cref="Messaging.ServiceBusProcessorHost"/>, need a real DI scope to resolve
    /// <see cref="TenantContext"/> and observe its effect on a scope-resolved
    /// <see cref="TestDbContext"/>'s query filter. The caller adds anything test-specific (a consumer,
    /// shared observation state) before calling <c>BuildServiceProvider()</c>.
    /// </summary>
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped(sp =>
        {
            var tenant = sp.GetRequiredService<ITenantContext>();
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlite(_connection)
                .Options;
            var context = new TestDbContext(options, tenant);
            context.Database.EnsureCreated();
            return context;
        });
        services.AddScoped<IInbox, Inbox<TestDbContext>>();
        services.AddScoped<IOutbox, Outbox<TestDbContext>>();
    }

    public void Dispose() => _connection.Dispose();
}
