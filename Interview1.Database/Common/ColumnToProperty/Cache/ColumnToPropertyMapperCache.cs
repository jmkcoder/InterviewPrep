using System.Collections.Concurrent;

namespace Interview1.Database.Common.ColumnToProperty.Cache
{
    public interface IColumnToPropertyMapperCache
    {
        ConcurrentDictionary<Type, PropertyMapping[]> PropertyMappingCache { get; }
        ConcurrentDictionary<string, Dictionary<string, int>> ColumnOrdinalCache { get; }
        void Clear();
    }

    public class ColumnToPropertyMapperCache : IColumnToPropertyMapperCache
    {
        public ConcurrentDictionary<Type, PropertyMapping[]> PropertyMappingCache { get; } = new();
        public ConcurrentDictionary<string, Dictionary<string, int>> ColumnOrdinalCache { get; } = new();

        public void Clear()
        {
            PropertyMappingCache.Clear();
            ColumnOrdinalCache.Clear();
        }
    }
}
