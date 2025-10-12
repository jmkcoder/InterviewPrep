using Interview1.Database;
using Interview1.Database.Repositories;
using Interview1.Employee.Dtos;
using Interview1.Employee.Repositories.StoredProcedures;

namespace Interview1.Employee.Repositories
{
    public interface IEmployeeRepository
    {
        Task<IAsyncEnumerable<EmployeeDto>> GetAllEmployeesAsync(CancellationToken cancellationToken = default);
        Task<EmployeeDto?> GetEmployeeByIdAsync(int employeeId, CancellationToken cancellationToken = default);
        IAsyncEnumerable<EmployeeDto> GetEmployeesStreamAsync(int? departmentId = null, CancellationToken cancellationToken = default);
    }

    public class EmployeeRepository : Repository, IEmployeeRepository
    {
        public EmployeeRepository(IDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<IAsyncEnumerable<EmployeeDto>> GetAllEmployeesAsync(CancellationToken cancellationToken = default)
        {
            var parameters = GetEmployeesStoredProc.Builder().Build();

            return await Execute<EmployeeDto>(GetEmployeesStoredProc.Name, parameters, cancellationToken);
        }

        public async Task<EmployeeDto?> GetEmployeeByIdAsync(int employeeId, CancellationToken cancellationToken = default)
        {
            var parameters = GetEmployeesStoredProc.Builder()
                .WithEmployeeId(employeeId)
                .Build();

            var employees = await Execute<EmployeeDto>(GetEmployeesStoredProc.Name, parameters, cancellationToken);
            return await employees.FirstOrDefaultAsync(cancellationToken);
        }

        public async IAsyncEnumerable<EmployeeDto> GetEmployeesStreamAsync(
            int? departmentId = null, 
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var builder = GetEmployeesStoredProc.Builder();

            if (departmentId.HasValue)
            {
                builder.WithDepartmentId(departmentId.Value);
            }
            var parameters = builder.Build();

            var employees = await Execute<EmployeeDto>(GetEmployeesStoredProc.Name, parameters, cancellationToken);

            // Stream directly without buffering - true zero-copy streaming
            await foreach (var employee in employees.WithCancellation(cancellationToken))
            {
                yield return employee;
            }
        }
    }
}