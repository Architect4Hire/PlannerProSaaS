using PlannerPro.Shared.Persistence;

namespace PlannerPro.Shared.Tests.TestSupport;

internal sealed class TestRepository(TestDbContext dbContext) : RepositoryBase<TestDbContext>(dbContext)
{
    public Task RunInTransactionAsync(Func<Task> operation) => ExecuteInTransactionAsync(operation);
}
