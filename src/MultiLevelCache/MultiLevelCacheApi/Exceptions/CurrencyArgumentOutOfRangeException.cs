namespace MultiLevelCacheApi.Exceptions
{
    public class CurrencyArgumentOutOfRangeException : ArgumentOutOfRangeException
    {
        public CurrencyArgumentOutOfRangeException()
        {
        }

        public CurrencyArgumentOutOfRangeException(string paramName)
            : base(paramName)
        {
        }

        public CurrencyArgumentOutOfRangeException(string paramName, string message)
            : base(paramName, message)
        {
        }

        public CurrencyArgumentOutOfRangeException(string message, Exception inner)
            : base(message, inner)
        {
        }

        public CurrencyArgumentOutOfRangeException(string paramName, object actualValue, string message)
            : base(paramName, actualValue, message)
        {
        }
    }


}
