namespace MultiLevelCacheApi.Exceptions
{
    public class ExRatesFetchException : Exception
    {
        public ExRatesFetchException()
        {
        }

        public ExRatesFetchException(string message)
            : base(message)
        {
        }

        public ExRatesFetchException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }


}
