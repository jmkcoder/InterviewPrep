using Interview1.Employee.Dtos;
using Interview1.Employee.Repositories;
using System.Runtime.CompilerServices;

namespace Interview1.Employee.Usecases
{
    public interface IGetEmployeesStreamUsecase
    {
        IAsyncEnumerable<EmployeeDto> ExecuteAsync(int? departmentId = null, CancellationToken cancellationToken = default);
    }

    public class GetEmployeesStreamUsecase : IGetEmployeesStreamUsecase
    {
        private readonly IEmployeeRepository _employeeRepository;

        public GetEmployeesStreamUsecase(IEmployeeRepository employeeRepository)
        {
            _employeeRepository = employeeRepository;
        }

        public async IAsyncEnumerable<EmployeeDto> ExecuteAsync(
            int? departmentId = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var employee in _employeeRepository.GetEmployeesStreamAsync(departmentId, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                yield return employee;
            }
        }
    }
}
