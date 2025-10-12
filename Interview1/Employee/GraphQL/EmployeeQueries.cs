using Interview1.Employee.Dtos;
using Interview1.Employee.Usecases;

namespace Interview1.Employee.GraphQL
{
    public class EmployeeQueries
    {
        /// <summary>
        /// Get all employees from the database
        /// </summary>
        [GraphQLDescription("Retrieves all employees")]
        public async Task<List<EmployeeDto>> GetEmployees(
            [Service] IGetEmployeesUsecase getEmployeesUsecase,
            CancellationToken cancellationToken)
        {
            var employees = await getEmployeesUsecase.GetAllEmployeesAsync(cancellationToken);
            return await employees.ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Get a specific employee by their employee number
        /// </summary>
        [GraphQLDescription("Retrieves a single employee by employee number")]
        public async Task<EmployeeDto?> GetEmployeeById(
            int employeeNo,
            [Service] IGetEmployeeByIdUsecase getEmployeeByIdUsecase,
            CancellationToken cancellationToken)
        {
            return await getEmployeeByIdUsecase.GetEmployeeByIdAsync(employeeNo, cancellationToken);
        }

        /// <summary>
        /// Get employees by department (streaming support)
        /// </summary>
        [GraphQLDescription("Retrieves employees by department, optionally filtered")]
        public async Task<List<EmployeeDto>> GetEmployeesByDepartment(
            int? departmentId,
            [Service] IGetEmployeesStreamUsecase getEmployeesStreamUsecase,
            CancellationToken cancellationToken)
        {
            var employees = new List<EmployeeDto>();
            await foreach (var employee in getEmployeesStreamUsecase.ExecuteAsync(departmentId, cancellationToken))
            {
                employees.Add(employee);
            }
            return employees;
        }
    }
}
