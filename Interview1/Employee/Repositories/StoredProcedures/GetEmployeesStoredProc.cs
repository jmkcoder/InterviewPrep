using Interview1.Database.Common;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace Interview1.Employee.Repositories.StoredProcedures
{
    public static class GetEmployeesStoredProc
    {
        public const string Name = "GetEmployees";

        private static class Parameters
        {
            public const string EmployeeNo = "p_employee_no";
            public const string DepartmentNo = "p_department_no";
            public const string Job = "p_job";
            public const string Cursor = "p_cursor";
        }

        public static ParameterBuilder Builder() => new();

        public class ParameterBuilder
        {
            private int? _employeeId;
            private int? _departmentId;
            private string? _job;

            public ParameterBuilder WithEmployeeId(int employeeId)
            {
                _employeeId = employeeId;
                return this;
            }

            public ParameterBuilder WithDepartmentId(int departmentId)
            {
                _departmentId = departmentId;
                return this;
            }

            public ParameterBuilder WithJob(string job)
            {
                if (job?.Length > 50)
                {
                    throw new ArgumentException("Job parameter cannot exceed 50 characters", nameof(job));
                }
                _job = job;
                return this;
            }

            public List<OracleStoredProcParameter> Build()
            {
                return CreateParameters(_employeeId, _departmentId, _job);
            }

            private static List<OracleStoredProcParameter> CreateParameters(int? employeeId = null, int? departmentId = null, string? job = null)
        {
            return
            [
                new OracleStoredProcParameter
                {
                    Name = Parameters.EmployeeNo,
                    Value = employeeId,
                    DbType = OracleDbType.Decimal,
                    Direction = ParameterDirection.Input
                },
                new OracleStoredProcParameter
                {
                    Name = Parameters.DepartmentNo,
                    Value = departmentId,
                    DbType = OracleDbType.Decimal,
                    Direction = ParameterDirection.Input
                },
                new OracleStoredProcParameter
                {
                    Name = Parameters.Job,
                    Value = job,
                    DbType = OracleDbType.Varchar2,
                    Direction = ParameterDirection.Input,
                    Size = 50
                },
                new OracleStoredProcParameter
                {
                    Name = Parameters.Cursor,
                    Value = null,
                    DbType = OracleDbType.RefCursor,
                    Direction = ParameterDirection.Output
                }
            ];
        }
        }
    }
}
