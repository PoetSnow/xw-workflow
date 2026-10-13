using Microsoft.Extensions.Options;
using XW.Infra.Redis.Configurations;
using System;

namespace XW.Infra.Redis.Caching.Core.BloomFilter;

public sealed class BloomFilterFactory(IEnumerable<IBloomFilter> instances, IOptions<RedisOptions> redisOptions)
{
    public IBloomFilter Create(string name)
    {
        IBloomFilter? bloomFilter;
        if (redisOptions.Value.EnableBloomFilter)
        {
            bloomFilter = instances.First(x => x.Name.EqualsIgnoreCase(name));
        }
        else
        {
            bloomFilter = instances.First(x => x.Name.EqualsIgnoreCase("null"));
        }

        return bloomFilter;
    }
}
