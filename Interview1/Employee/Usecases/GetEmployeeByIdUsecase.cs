using Interview1.Employee.Dtos;
using Interview1.Employee.Repositories;
using Interview1.Repositories.Dtos;

namespace Interview1.Employee.Usecases
{
    public interface IGetEmployeeByIdUsecase
    {
        Task<EmployeeDto?> GetEmployeeByIdAsync(int employeeNo, CancellationToken cancellationToken = default);
    }

    public class GetEmployeeByIdUsecase : IGetEmployeeByIdUsecase
    {
        private readonly IEmployeeRepository _employeeRepository;

        public GetEmployeeByIdUsecase(IEmployeeRepository employeeRepository)
        {
            _employeeRepository = employeeRepository;
        }

        public async Task<EmployeeDto?> GetEmployeeByIdAsync(int employeeNo, CancellationToken cancellationToken = default)
        {
            return await _employeeRepository.GetEmployeeByIdAsync(employeeNo, cancellationToken);
        }
    }
}