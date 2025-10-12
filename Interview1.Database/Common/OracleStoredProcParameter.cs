using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace Interview1.Database.Common
{
    public class OracleStoredProcParameter
    {
        public string Name { get; set; } = string.Empty;
        public object? Value { get; set; }
        public OracleDbType DbType { get; set; }
        public ParameterDirection Direction { get; set; } = ParameterDirection.Input;
        public int? Size { get; set; }

        public OracleParameter ToOracleParameter()
        {
            var parameter = new OracleParameter(Name, DbType)
            {
                Direction = Direction,
                Value = Value ?? DBNull.Value
            };

            if (Size.HasValue)
            {
                parameter.Size = Size.Value;
            }

            return parameter;
        }
    }
}