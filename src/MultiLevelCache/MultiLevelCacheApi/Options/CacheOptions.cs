namespace MultiLevelCacheApi.Options
{
    public class CacheOptions
    {
        public string RedisConnectionString { get; set; } = string.Empty;
        public string KeyPrefix { get; set; } = string.Empty;
        public TimeSpan TimeToLiveDefault { get; set; }
        public TimeSpan StaleAfterDefault { get; set; }
        /// <summary>
        /// Used in the event of cachetower failure to cache backing store calls in memory
        /// </summary>
        public TimeSpan StoreBufferDefault { get; set; }
    }

}