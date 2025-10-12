using Interview1.Employee.Repositories;
using Interview1.Employee.Usecases;

namespace Interview1.Employee
{
    public static class EmployeeServiceConfigurator
    {
        public static void AddEmployeeServices(this IServiceCollection services)
        {
            services.AddScoped<IGetEmployeesUsecase, GetEmployeesUsecase>();
            services.AddScoped<IGetEmployeeByIdUsecase, GetEmployeeByIdUsecase>();
            services.AddScoped<IGetEmployeesStreamUsecase, GetEmployeesStreamUsecase>();

            services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        }
    }
}
