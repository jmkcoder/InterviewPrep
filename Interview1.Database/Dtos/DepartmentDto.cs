using Interview1.Database.Common;
using Interview1.Database.Common.Attributes;

namespace Interview1.Repositories.Dtos
{
    public class DepartmentDto : IEntity
    {
        [ColumnName("DEPTNO")]
        public int DepartmentNo { get; set; }

        [ColumnName("DNAME")]
        public string? Name { get; set; }

        [ColumnName("LOC")]
        public string? Location { get; set; }
    }
}
