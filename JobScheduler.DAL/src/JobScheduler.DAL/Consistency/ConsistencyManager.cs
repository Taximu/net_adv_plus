using Microsoft.Extensions.Caching.Memory;

namespace JobScheduler.DAL.Consistency;

/// <summary>
/// Tracks recent writes per logical actor (for example a user id string) so the BL can upgrade
/// the next reads to <see cref="ConsistencyLevel.Strong"/> (primary) without exposing levels to APIs.
/// </summary>
public class ConsistencyManager
{
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cooldownPeriod;
    private const string CacheKeyPrefix = "UserLastWrite_";

    public ConsistencyManager(IMemoryCache cache, TimeSpan? cooldownPeriod = null)
    {
        _cache = cache;
        _cooldownPeriod = cooldownPeriod ?? TimeSpan.FromSeconds(5);
    }

    /// <summary>Records that a write occurred for the actor and starts the cooldown window.</summary>
    public void TrackWrite(string userId)
    {
        var cacheKey = GetCacheKey(userId);
        _cache.Set(cacheKey, DateTime.UtcNow, _cooldownPeriod);
    }

    /// <summary>True when the actor is still inside the cooldown and reads should use read-after-write routing.</summary>
    public bool IsReadAfterWriteApplicable(string userId)
    {
        if (_cache.TryGetValue(GetCacheKey(userId), out DateTime lastWriteTime))
            return (DateTime.UtcNow - lastWriteTime) < _cooldownPeriod;

        return false;
    }

    private string GetCacheKey(string userId) => $"{CacheKeyPrefix}{userId}";
}
