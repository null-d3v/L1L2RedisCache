using System;

namespace L1L2RedisCache
{
    public class CacheMessage
    {
        public string? Key { get; set; }
        public Guid PublisherId { get; set; }
    }
}
