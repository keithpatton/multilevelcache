namespace MultiLevelCacheApi.Options
{
    public class CacheOptions
    {
        public string RedisConnectionString { get; set; } = string.Empty;
        public string KeyPrefix { get; set; } = string.Empty;
        public TimeSpan TimeToLiveDefault { get; set; }
        public TimeSpan StaleAfterDefault { get; set; }
        public TimeSpan StoreBufferDefault { get; set; } = TimeSpan.FromMinutes(60);

    }

}