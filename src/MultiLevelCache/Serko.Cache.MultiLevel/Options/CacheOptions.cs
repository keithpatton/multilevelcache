namespace Serko.Cache.MultiLevel.Options
{
    /// <summary>
    /// Represents the configuration settings for caching.
    /// </summary>
    public record CacheOptions
    {

        /// <summary>
        /// Gets or sets the prefix for cache keys.
        /// This prefix is used to namespace cache entries.
        /// </summary>
        /// <value>
        /// The cache key prefix, which should be for example the unique name of your service
        /// </value>
        public string KeyPrefix { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the default time-to-live (TTL) for cache entries.
        /// Determines how long an entry stays in the cache before expiring.
        /// </summary>
        /// <value>
        /// The default TTL as a <see cref="TimeSpan"/>.
        /// </value>
        public TimeSpan TimeToLiveDefault { get; set; }

        /// <summary>
        /// Gets or sets the default time for a cache entry to become stale.
        /// A thread-safe background fetch is performed when this value is set to keep cache up to date
        /// </summary>
        /// <value>
        /// The stale time as a nullable <see cref="TimeSpan"/>.
        /// </value>
        public TimeSpan? StaleAfterDefault { get; set; }

        /// <summary>
        /// Gets or sets the default time for storing entries in a local memory buffer.
        /// This can help reduce load on your backing store in the event of the core multi-level cache call failing
        /// </summary>
        /// <remarks>
        /// It is advisable to set this value if your backing store is for example an api where excess calls during exceptions may cause problems.
        /// </remarks>
        /// <value>
        /// The store buffer time as a nullable <see cref="TimeSpan"/>.
        /// </value>
        public TimeSpan? StoreBufferDefault { get; set; }

    }

}