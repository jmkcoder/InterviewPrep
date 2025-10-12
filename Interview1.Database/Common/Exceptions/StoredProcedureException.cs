namespace Interview1.Database.Common.Exceptions
{
    public class StoredProcedureException : DatabaseException
    {
        public string? ProcedureName { get; }

        public StoredProcedureException(string message, string? procedureName = null) : base(message)
        {
            ProcedureName = procedureName;
        }

        public StoredProcedureException(string message, Exception innerException, string? procedureName = null)
            : base(message, innerException)
        {
            ProcedureName = procedureName;
        }
    }
}
