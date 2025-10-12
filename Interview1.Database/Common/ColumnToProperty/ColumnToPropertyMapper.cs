using Interview1.Database.Common.Attributes;
using Interview1.Database.Common.ColumnToProperty.Cache;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Interview1.Database.Common.ColumnToProperty
{
    public static class ColumnToPropertyMapper
    {
        public static T Map<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(this IDataRecord record, T entity) where T : IEntity
        {
            var cache = ServiceLocator.GetService<IColumnToPropertyMapperCache>();
            return Map(record, entity, cache);
        }

        public static T Map<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(this IDataRecord record, T entity, IColumnToPropertyMapperCache cache) where T : IEntity
        {
            try
            {
                var entityType = typeof(T);
                var propertyMappings = GetCachedPropertyMappings(entityType, cache);
                var columnOrdinals = GetColumnOrdinals(record, propertyMappings, cache);

                foreach (var mapping in propertyMappings)
                {
                    try
                    {
                        if (!columnOrdinals.TryGetValue(mapping.ColumnName, out var ordinal))
                        {
                            // Column doesn't exist in result set - skip silently or log if needed
                            continue;
                        }

                        if (record.IsDBNull(ordinal))
                        {
                            // Handle null values for nullable types
                            if (mapping.IsNullable)
                            {
                                mapping.Property.SetValue(entity, null);
                            }

                            // For non-nullable types, leave default value
                            continue;
                        }

                        var value = record.GetValue(ordinal);
                        var convertedValue = ConvertValue(value, mapping.Property.PropertyType, mapping.ColumnName, mapping.Property.Name);
                        mapping.Property.SetValue(entity, convertedValue);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"Error mapping column '{mapping.ColumnName}' to property '{mapping.Property.Name}' of type '{mapping.Property.PropertyType.Name}' on entity '{entityType.Name}'. " +
                            $"Column value: '{(record.IsDBNull(columnOrdinals[mapping.ColumnName]) ? "NULL" : record.GetValue(columnOrdinals[mapping.ColumnName]))}'",
                            ex);
                    }
                }

                return entity;
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException($"Error mapping data record to entity of type '{typeof(T).Name}'", ex);
            }
        }

        private static PropertyMapping[] GetCachedPropertyMappings([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type entityType, IColumnToPropertyMapperCache cache)
        {
            return cache.PropertyMappingCache.GetOrAdd(entityType, ([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type) =>
            {
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanWrite)
                    .Select(prop =>
                    {
                        var attr = prop.GetCustomAttribute<ColumnNameAttribute>();
                        if (attr != null)
                        {
                            var underlyingType = Nullable.GetUnderlyingType(prop.PropertyType);
                            return new PropertyMapping
                            {
                                Property = prop,
                                ColumnName = attr.Name,
                                IsNullable = underlyingType != null || !prop.PropertyType.IsValueType,
                                UnderlyingType = underlyingType ?? prop.PropertyType
                            };
                        }
                        return null;
                    })
                    .Where(mapping => mapping != null)
                    .Cast<PropertyMapping>()
                    .ToArray();

                return properties;
            });
        }

        private static Dictionary<string, int> GetColumnOrdinals(IDataRecord record, PropertyMapping[] mappings, IColumnToPropertyMapperCache cache)
        {
            var columnNames = new string[record.FieldCount];

            for (int i = 0; i < record.FieldCount; i++)
            {
                columnNames[i] = record.GetName(i);
            }

            var columnNamesKey = string.Join("|", columnNames.OrderBy(x => x));
            var propertyMappingsKey = string.Join("|", mappings.Select(m => m.ColumnName).OrderBy(x => x));
            var cacheKey = $"{columnNamesKey}#{propertyMappingsKey}";

            return cache.ColumnOrdinalCache.GetOrAdd(cacheKey, _ =>
            {
                var ordinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                
                foreach (var mapping in mappings)
                {
                    try
                    {
                        var ordinal = record.GetOrdinal(mapping.ColumnName);
                        ordinals[mapping.ColumnName] = ordinal;
                    }
                    catch (IndexOutOfRangeException)
                    {
                        // Column doesn't exist - will be handled in main mapping loop
                    }
                }
                
                return ordinals;
            });
        }

        private static object? ConvertValue(object value, Type targetType, string columnName, string propertyName)
        {
            if (value == null || value == DBNull.Value)
                return null;

            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (underlyingType == value.GetType())
                return value;

            try
            {
                if (underlyingType.IsEnum)
                {
                    if (value is string stringValue)
                        return Enum.Parse(underlyingType, stringValue, true);
                    else
                        return Enum.ToObject(underlyingType, value);
                }

                if (underlyingType == typeof(Guid))
                {
                    if (value is string guidString)
                        return Guid.Parse(guidString);
                    if (value is byte[] guidBytes)
                        return new Guid(guidBytes);
                }

                if (underlyingType == typeof(TimeSpan) && value is decimal decimalValue)
                {
                    return TimeSpan.FromDays((double)decimalValue);
                }

                return Convert.ChangeType(value, underlyingType);
            }
            catch (Exception ex)
            {
                throw new InvalidCastException(
                    $"Cannot convert value '{value}' of type '{value.GetType().Name}' from column '{columnName}' to property '{propertyName}' of type '{targetType.Name}'",
                    ex);
            }
        }

        public static void ClearCache()
        {
            var cache = ServiceLocator.GetService<IColumnToPropertyMapperCache>();
            ClearCache(cache);
        }

        private static void ClearCache(IColumnToPropertyMapperCache cache)
        {
            cache.Clear();
        }
    }
}
