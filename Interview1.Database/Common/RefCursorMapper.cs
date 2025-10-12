using Interview1.Database.Common.ColumnToProperty;
using Interview1.Database.Common.ColumnToProperty.Cache;
using Interview1.Database.Common.Exceptions;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Interview1.Database.Common
{
    public static class RefCursorMapper<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T> where T : IEntity, new()
    {
        public static async IAsyncEnumerable<T> MapWithDisposal(
            IDataReader reader, 
            IDisposable command, 
            IColumnToPropertyMapperCache cache)
        {
            try
            {
                await foreach (var item in MapInternal(reader, cache))
                {
                    yield return item;
                }
            }
            finally
            {
                reader?.Dispose();
                command?.Dispose();
            }
        }

        public static async IAsyncEnumerable<T> Map(IDataReader reader, IColumnToPropertyMapperCache cache)
        {
            await foreach (var item in MapInternal(reader, cache))
            {
                yield return item;
            }
        }

        private static async IAsyncEnumerable<T> MapInternal(IDataReader reader, IColumnToPropertyMapperCache cache)
        {
            int rowNumber = 0;
            List<string>? availableColumns = null;

            availableColumns = GetAvailableColumns(reader);
            
            while (reader.Read())
            {
                rowNumber++;
                T entity;
                
                try
                {
                    entity = new T();
                    reader.Map(entity, cache);
                }
                catch (DataMappingException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    var errorMessage = BuildRowErrorMessage(rowNumber, availableColumns, ex);
                    throw new DataMappingException(errorMessage, ex, typeof(T).Name);
                }
                
                // Yield each item immediately for true streaming
                yield return entity;
                
                // Allow async context switching for better performance
                await Task.Yield();
            }
        }

        private static List<string> GetAvailableColumns(IDataReader reader)
        {
            var columns = new List<string>();
            try
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var columnName = reader.GetName(i);
                    var columnType = reader.GetFieldType(i).Name;
                    columns.Add($"{columnName} ({columnType})");
                }
            }
            catch
            {
                // If we can't get column info, just return empty list
            }
            return columns;
        }

        private static string BuildRowErrorMessage(int rowNumber, List<string> availableColumns, Exception ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Error mapping row {rowNumber} to entity of type '{typeof(T).Name}'.");
            
            if (availableColumns.Count != 0)
            {
                sb.AppendLine($"Available columns in result set:");
                foreach (var column in availableColumns)
                {
                    sb.AppendLine($"  - {column}");
                }
            }
            
            sb.AppendLine($"Error details: {ex.Message}");
            
            return sb.ToString();
        }
    }
}
