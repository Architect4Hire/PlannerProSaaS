using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

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

    public TestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .Options;

        var context = new TestDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    public void Dispose() => _connection.Dispose();
}
