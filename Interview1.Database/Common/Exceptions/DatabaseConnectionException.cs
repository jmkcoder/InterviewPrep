namespace Interview1.Database.Common.Exceptions
{
    public class DatabaseConnectionException : DatabaseException
    {
        public string? ConnectionString { get; }

        public DatabaseConnectionException(string message, string? connectionString = null) : base(message)
        {
            ConnectionString = connectionString;
        }

        public DatabaseConnectionException(string message, Exception innerException, string? connectionString = null)
            : base(message, innerException)
        {
            ConnectionString = connectionString;
        }
    }
}
