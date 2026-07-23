using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace PlannerPro.Shared.Caching;

public sealed class DistributedCacheService(IDistributedCache cache) : ICacheService
{
    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var bytes = await cache.GetAsync(key, ct);
        return bytes is null ? default : JsonSerializer.Deserialize<T>(bytes);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken ct = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiration };
        return cache.SetAsync(key, bytes, options, ct);
    }

    public Task RemoveAsync(string key, CancellationToken ct = default) =>
        cache.RemoveAsync(key, ct);
}
