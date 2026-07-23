using Microsoft.EntityFrameworkCore;
using PlannerPro.Shared.Persistence;
using PlannerPro.Shared.Tests.TestSupport;

namespace PlannerPro.Shared.Tests.Persistence;

public sealed class RepositoryBaseTests : IDisposable
{
    private readonly SqliteTestDbContextFactory _factory = new();

    [Fact]
    public async Task ExecuteInTransactionAsync_RollsBackWhenTheOperationThrowsMidway()
    {
        await using var context = _factory.CreateContext();
        var repository = new TestRepository(context);
        var tenantId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.RunInTransactionAsync(async () =>
        {
            context.OutboxMessages.Add(NewOutboxMessage(tenantId));
            await context.SaveChangesAsync(); // flush the insert under the open transaction before throwing
            throw new InvalidOperationException("boom mid-operation");
        }));

        Assert.Equal(0, await context.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_CommitsWhenTheOperationSucceeds()
    {
        await using var context = _factory.CreateContext();
        var repository = new TestRepository(context);
        var tenantId = Guid.NewGuid();

        await repository.RunInTransactionAsync(() =>
        {
            context.OutboxMessages.Add(NewOutboxMessage(tenantId));
            return Task.CompletedTask;
        });

        Assert.Equal(1, await context.OutboxMessages.CountAsync());
    }

    private static OutboxMessage NewOutboxMessage(Guid tenantId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Type = "test",
        Content = "{}",
        CorrelationId = Guid.NewGuid(),
        ActorId = Guid.NewGuid(),
        OccurredOnUtc = DateTime.UtcNow,
    };

    public void Dispose() => _factory.Dispose();
}
