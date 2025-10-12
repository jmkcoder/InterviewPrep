using System.Reflection;

namespace Interview1.Database.Common.ColumnToProperty.Cache
{
    public class PropertyMapping
    {
        public PropertyInfo Property { get; set; } = null!;
        public string ColumnName { get; set; } = null!;
        public bool IsNullable { get; set; }
        public Type UnderlyingType { get; set; } = null!;
    }
}
