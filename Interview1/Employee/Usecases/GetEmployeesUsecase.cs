using Interview1.Employee.Dtos;
using Interview1.Employee.Repositories;

namespace Interview1.Employee.Usecases
{
    public interface IGetEmployeesUsecase
    {
        Task<IAsyncEnumerable<EmployeeDto>> GetAllEmployeesAsync(CancellationToken cancellationToken = default);
    }

    public class GetEmployeesUsecase : IGetEmployeesUsecase
    {
        private readonly IEmployeeRepository _employeeRepository;

        public GetEmployeesUsecase(IEmployeeRepository employeeRepository)
        {
            _employeeRepository = employeeRepository;
        }

        public async Task<IAsyncEnumerable<EmployeeDto>> GetAllEmployeesAsync(CancellationToken cancellationToken = default)
        {
            return await _employeeRepository.GetAllEmployeesAsync(cancellationToken);
        }
    }
}