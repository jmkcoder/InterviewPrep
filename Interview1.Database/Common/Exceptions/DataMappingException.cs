namespace Interview1.Database.Common.Exceptions
{
    public class DataMappingException : DatabaseException
    {
        public string? EntityType { get; }
        public string? ColumnName { get; }

        public DataMappingException(string message, string? entityType = null, string? columnName = null)
            : base(message)
        {
            EntityType = entityType;
            ColumnName = columnName;
        }

        public DataMappingException(string message, Exception innerException, string? entityType = null, string? columnName = null)
            : base(message, innerException)
        {
            EntityType = entityType;
            ColumnName = columnName;
        }
    }
}
