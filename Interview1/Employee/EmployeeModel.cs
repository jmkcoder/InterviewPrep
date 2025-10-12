using System.ComponentModel.DataAnnotations;

namespace Interview1.Employee
{
    public class EmployeeModel
    {
        [Required]
        public string? Name { get; set; }
    }
}
