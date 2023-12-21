namespace MultiLevelCacheApi.Exceptions
{
    public class CurrencyFetchException : Exception
    {
        public CurrencyFetchException()
        {
        }

        public CurrencyFetchException(string message)
            : base(message)
        {
        }

        public CurrencyFetchException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }


}
