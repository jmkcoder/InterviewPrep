namespace Interview1.Database.Common.Exceptions
{
    public class TransactionException : DatabaseException
    {
        public TransactionException(string message) : base(message)
        {
        }

        public TransactionException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
