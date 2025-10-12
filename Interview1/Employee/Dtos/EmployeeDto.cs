using HotChocolate;
using Interview1.Database.Common;
using Interview1.Database.Common.Attributes;

namespace Interview1.Employee.Dtos
{
    [GraphQLDescription("Represents an employee in the organization")]
    public class EmployeeDto : IEntity
    {
        [ColumnName("EMPNO")]
        [GraphQLDescription("The unique employee number")]
        public int EmployeeNo { get; set; }

        [ColumnName("ENAME")]
        [GraphQLDescription("The employee's name")]
        public string? Name { get; set; }

        [ColumnName("JOB")]
        [GraphQLDescription("The employee's job title")]
        public string? Job { get; set; }

        [ColumnName("MGR")]
        [GraphQLDescription("The employee number of the employee's manager")]
        public int? Manager { get; set; }

        [ColumnName("HIREDATE")]
        [GraphQLDescription("The date the employee was hired")]
        public DateTime HireDate { get; set; }

        [ColumnName("SAL")]
        [GraphQLDescription("The employee's salary")]
        public decimal Salary { get; set; }

        [ColumnName("COMM")]
        [GraphQLDescription("The employee's commission (if applicable)")]
        public decimal? Commission { get; set; }

        [ColumnName("DEPTNO")]
        [GraphQLDescription("The department number the employee belongs to")]
        public int DepartmentNo { get; set; }
    }
}
