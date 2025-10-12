using Interview1.Employee.Dtos;
using Interview1.Employee.Usecases;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Interview1.Employee
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmployeeController : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IEnumerable<EmployeeDto>>> GetAllEmployees(CancellationToken cancellationToken, [FromServices] IGetEmployeesUsecase getEmployeesUsecase)
        {
            // Materialize the async enumerable into a list for non-streaming endpoint
            var employees = await getEmployeesUsecase.GetAllEmployeesAsync(cancellationToken);
            var employeeList = await employees.ToListAsync(cancellationToken);
            return Ok(employeeList);
        }

        [HttpGet("{employeeNo}")]
        public async Task<ActionResult<IEnumerable<EmployeeDto>>> GetAllEmployees(int employeeNo, CancellationToken cancellationToken, [FromServices]  IGetEmployeeByIdUsecase getEmployeeByIdUsecase)
        {
            var employees = await getEmployeeByIdUsecase.GetEmployeeByIdAsync(employeeNo, cancellationToken);
            return Ok(employees);
        }

        [HttpPost]
        public async Task<ActionResult> SaveEmployee(EmployeeModel employee)
        {
            if (!ModelState.IsValid)
                return BadRequest(new ValidationProblemDetails(ModelState));

            return Ok();
        }

        [HttpGet("stream")]
        public async IAsyncEnumerable<EmployeeDto> StreamEmployeesAsync(
            [FromQuery] int? departmentId,
            [EnumeratorCancellation] CancellationToken cancellationToken,
            [FromServices] IGetEmployeesStreamUsecase getEmployeesStreamUsecase)
        {
            await foreach (var employee in getEmployeesStreamUsecase.ExecuteAsync(departmentId, cancellationToken))
            {
                yield return employee;
            }
        }
    }
}