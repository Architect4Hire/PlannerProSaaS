using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PlannerPro.Access.Core.Data;
using PlannerPro.Shared.Tenancy;

namespace PlannerPro.Access.Tests.TestSupport;

/// <summary>
/// Mirrors <c>PlannerPro.Shared.Tests.TestSupport.SqliteTestDbContextFactory</c> — a real (if
/// lightweight) relational provider is needed because EF Core's InMemory provider doesn't support
/// transactions/execution strategies, which <c>RepositoryBase.ExecuteInTransactionAsync</c> depends on.
/// </summary>
internal sealed class SqliteAccessDbContextFactory : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public SqliteAccessDbContextFactory()
    {
        _connection.Open();
    }

    /// <summary>Exposed so tests wiring a full DI container (e.g. for Identity's
    /// <c>UserManager</c>/<c>SignInManager</c>) can register <c>AccessDbContext</c> against the same
    /// shared in-memory database via their own <c>AddDbContext</c> call.</summary>
    public SqliteConnection Connection => _connection;

    public AccessDbContext CreateContext(ITenantContext? tenant = null)
    {
        var tenantContext = tenant ?? StaticTenantContext.Bypass;

        // No .AddInterceptors(...) here — AccessDbContext's base (SharedDbContext) wires
        // TenantSaveChangesInterceptor itself from the tenantContext passed to the constructor below.
        var options = new DbContextOptionsBuilder<AccessDbContext>()
            .UseSqlite(_connection)
            .Options;

        var context = new AccessDbContext(options, tenantContext);
        context.Database.EnsureCreated();
        return context;
    }

    public void Dispose() => _connection.Dispose();
}
