using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PlannerPro.Portfolio.Core.Data;
using PlannerPro.Shared.Tenancy;

namespace PlannerPro.Portfolio.Tests.TestSupport;

/// <summary>Mirrors <c>PlannerPro.Access.Tests.TestSupport.SqliteAccessDbContextFactory</c> — a real
/// (if lightweight) relational provider is needed because EF Core's InMemory provider doesn't support
/// transactions/execution strategies, which <c>RepositoryBase.ExecuteInTransactionAsync</c> depends on.</summary>
internal sealed class SqlitePortfolioDbContextFactory : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public SqlitePortfolioDbContextFactory()
    {
        _connection.Open();
    }

    public PortfolioDbContext CreateContext(ITenantContext? tenant = null)
    {
        var tenantContext = tenant ?? StaticTenantContext.Bypass;

        var options = new DbContextOptionsBuilder<PortfolioDbContext>()
            .UseSqlite(_connection)
            .Options;

        var context = new PortfolioDbContext(options, tenantContext);
        context.Database.EnsureCreated();
        return context;
    }

    public void Dispose() => _connection.Dispose();
}
