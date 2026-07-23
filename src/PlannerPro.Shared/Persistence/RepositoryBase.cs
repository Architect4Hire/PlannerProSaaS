using Microsoft.EntityFrameworkCore;

namespace PlannerPro.Shared.Persistence;

public abstract class RepositoryBase<TContext>(TContext context) where TContext : DbContext
{
    protected readonly TContext Context = context;

    protected async Task ExecuteInTransactionAsync(Func<Task> operation)
    {
        var strategy = Context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await Context.Database.BeginTransactionAsync();
            await operation();
            await Context.SaveChangesAsync();
            await transaction.CommitAsync();
        });
    }

    protected async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> operation)
    {
        var strategy = Context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await Context.Database.BeginTransactionAsync();
            var result = await operation();
            await Context.SaveChangesAsync();
            await transaction.CommitAsync();
            return result;
        });
    }
}
