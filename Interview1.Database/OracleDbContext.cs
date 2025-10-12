using Interview1.Database.Common;
using Interview1.Database.Common.ColumnToProperty.Cache;
using Interview1.Database.Common.Exceptions;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace Interview1.Database
{
    public interface IDbContext : IDisposable
    {
        int CommandTimeout { get; set; }
        Task<IAsyncEnumerable<T>> ExecuteStoredProcedureAsync<T>(string procedureName, IEnumerable<OracleStoredProcParameter> parameters, CancellationToken cancellationToken = default) where T : IEntity, new();
        Task ExecuteStoredProcedureAsync(string procedureName, IEnumerable<OracleStoredProcParameter> parameters, CancellationToken cancellationToken = default);
        Task<IDataReader> ExecuteReaderAsync(string sql, IEnumerable<OracleStoredProcParameter> parameters, CancellationToken cancellationToken = default);
        IDbContextTransaction BeginTransaction();
        Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
        Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
    }

    public class OracleDbContext : IDbContext
    {
        private readonly string _connectionString;
        private readonly IColumnToPropertyMapperCache _cache;
        private OracleConnection? _connection;
        private OracleTransaction? _currentTransaction;
        private bool _disposed;
        private const int MaxRetryAttempts = 3;
        private const int RetryDelayMilliseconds = 1000;

        public int CommandTimeout { get; set; } = 30;

        public OracleDbContext(IConfiguration configuration, IColumnToPropertyMapperCache cache)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new InvalidOperationException("Database connection string 'DefaultConnection' not found.");
            _cache = cache;
        }

        public async Task<IAsyncEnumerable<T>> ExecuteStoredProcedureAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
            string procedureName, 
            IEnumerable<OracleStoredProcParameter> parameters,
            CancellationToken cancellationToken = default) where T : IEntity, new()
        {
            return await ExecuteWithRetryAsync<IAsyncEnumerable<T>>(async () =>
            {
                try
                {
                    var connection = await GetOrCreateConnectionAsync(cancellationToken);
                    
                    var command = new OracleCommand(procedureName, connection)
                    {
                        CommandType = CommandType.StoredProcedure,
                        CommandTimeout = CommandTimeout
                    };
                    
                    foreach (var param in parameters)
                    {
                        command.Parameters.Add(param.ToOracleParameter());
                    }

                    await command.ExecuteNonQueryAsync(cancellationToken);
                    
                    var cursorParam = command.Parameters.Cast<OracleParameter>()
                        .FirstOrDefault(p => p.Direction == ParameterDirection.Output && p.OracleDbType == OracleDbType.RefCursor);
                    
                    if (cursorParam?.Value is OracleRefCursor refCursor)
                    {
                        var reader = refCursor.GetDataReader();
                        // Keep reader and command alive - they'll be disposed when enumeration completes
                        return RefCursorMapper<T>.MapWithDisposal(reader, command, _cache);
                    }
                    
                    command.Dispose();
                    return new List<T>().ToAsyncEnumerable();
                }
                catch (OracleException ex)
                {
                    throw new StoredProcedureException(
                        $"Failed to execute stored procedure '{procedureName}'. Oracle Error: {ex.Message}", 
                        ex, 
                        procedureName);
                }
                catch (Exception ex) when (ex is not DatabaseException)
                {
                    throw new StoredProcedureException(
                        $"Unexpected error executing stored procedure '{procedureName}'.", 
                        ex, 
                        procedureName);
                }
            }, cancellationToken);
        }

        public async Task ExecuteStoredProcedureAsync(
            string procedureName, 
            IEnumerable<OracleStoredProcParameter> parameters,
            CancellationToken cancellationToken = default)
        {
            await ExecuteActionWithRetryAsync(async () =>
            {
                try
                {
                    var connection = await GetOrCreateConnectionAsync(cancellationToken);
                    
                    using var command = new OracleCommand(procedureName, connection)
                    {
                        CommandType = CommandType.StoredProcedure,
                        CommandTimeout = CommandTimeout
                    };
                    
                    foreach (var param in parameters)
                    {
                        command.Parameters.Add(param.ToOracleParameter());
                    }

                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
                catch (OracleException ex)
                {
                    throw new StoredProcedureException(
                        $"Failed to execute stored procedure '{procedureName}'. Oracle Error: {ex.Message}", 
                        ex, 
                        procedureName);
                }
                catch (Exception ex) when (ex is not DatabaseException)
                {
                    throw new StoredProcedureException(
                        $"Unexpected error executing stored procedure '{procedureName}'.", 
                        ex, 
                        procedureName);
                }
            }, cancellationToken);
        }

        public async Task<IDataReader> ExecuteReaderAsync(
            string sql,
            IEnumerable<OracleStoredProcParameter> parameters,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                try
                {
                    var connection = await GetOrCreateConnectionAsync(cancellationToken);
                    var command = new OracleCommand(sql, connection)
                    {
                        CommandTimeout = CommandTimeout
                    };

                    foreach (var param in parameters)
                    {
                        command.Parameters.Add(param.ToOracleParameter());
                    }

                    return await command.ExecuteReaderAsync(cancellationToken);
                }
                catch (OracleException ex)
                {
                    throw new DatabaseException($"Failed to execute SQL query. Oracle Error: {ex.Message}", ex);
                }
                catch (Exception ex) when (ex is not DatabaseException)
                {
                    throw new DatabaseException($"Unexpected error executing SQL query.", ex);
                }
            }, cancellationToken);
        }

        public IDbContextTransaction BeginTransaction()
        {
            EnsureConnectionOpen();
            
            if (_currentTransaction != null)
                throw new TransactionException("A transaction is already in progress.");

            try
            {
                _currentTransaction = _connection!.BeginTransaction();
                return new OracleDbContextTransaction(_currentTransaction);
            }
            catch (Exception ex)
            {
                throw new TransactionException("Failed to begin transaction.", ex);
            }
        }

        public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            await EnsureConnectionOpenAsync(cancellationToken);
            
            if (_currentTransaction != null)
                throw new TransactionException("A transaction is already in progress.");

            try
            {
                _currentTransaction = _connection!.BeginTransaction();
                return new OracleDbContextTransaction(_currentTransaction);
            }
            catch (Exception ex)
            {
                throw new TransactionException("Failed to begin transaction.", ex);
            }
        }

        public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var connection = new OracleConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                return connection.State == ConnectionState.Open;
            }
            catch
            {
                return false;
            }
        }

        private async Task<OracleConnection> GetOrCreateConnectionAsync(CancellationToken cancellationToken)
        {
            if (_currentTransaction != null && _connection != null)
            {
                return _connection;
            }

            if (_connection == null)
            {
                _connection = new OracleConnection(_connectionString);
            }

            if (_connection.State != ConnectionState.Open)
            {
                try
                {
                    await _connection.OpenAsync(cancellationToken);
                }
                catch (OracleException ex)
                {
                    throw new DatabaseConnectionException(
                        $"Failed to open database connection. Oracle Error: {ex.Message}", 
                        ex,
                        MaskConnectionString(_connectionString));
                }
                catch (Exception ex)
                {
                    throw new DatabaseConnectionException(
                        "Unexpected error opening database connection.", 
                        ex,
                        MaskConnectionString(_connectionString));
                }
            }

            return _connection;
        }

        private void EnsureConnectionOpen()
        {
            if (_connection == null)
            {
                _connection = new OracleConnection(_connectionString);
            }

            if (_connection.State != ConnectionState.Open)
            {
                try
                {
                    _connection.Open();
                }
                catch (OracleException ex)
                {
                    throw new DatabaseConnectionException(
                        $"Failed to open database connection. Oracle Error: {ex.Message}", 
                        ex,
                        MaskConnectionString(_connectionString));
                }
            }
        }

        private async Task EnsureConnectionOpenAsync(CancellationToken cancellationToken)
        {
            if (_connection == null)
            {
                _connection = new OracleConnection(_connectionString);
            }

            if (_connection.State != ConnectionState.Open)
            {
                try
                {
                    await _connection.OpenAsync(cancellationToken);
                }
                catch (OracleException ex)
                {
                    throw new DatabaseConnectionException(
                        $"Failed to open database connection. Oracle Error: {ex.Message}", 
                        ex,
                        MaskConnectionString(_connectionString));
                }
            }
        }

        private async Task<TResult> ExecuteWithRetryAsync<TResult>(
            Func<Task<TResult>> operation,
            CancellationToken cancellationToken)
        {
            int attempt = 0;
            while (true)
            {
                try
                {
                    return await operation();
                }
                catch (OracleException ex) when (IsTransientError(ex) && attempt < MaxRetryAttempts)
                {
                    attempt++;
                    if (attempt >= MaxRetryAttempts)
                        throw;

                    await Task.Delay(RetryDelayMilliseconds * attempt, cancellationToken);
                    
                    if (_connection != null && _currentTransaction == null)
                    {
                        _connection.Dispose();
                        _connection = null;
                    }
                }
            }
        }

        private async Task ExecuteActionWithRetryAsync(
            Func<Task> operation,
            CancellationToken cancellationToken)
        {
            int attempt = 0;
            while (true)
            {
                try
                {
                    await operation();
                }
                catch (OracleException ex) when (IsTransientError(ex) && attempt < MaxRetryAttempts)
                {
                    attempt++;
                    if (attempt >= MaxRetryAttempts)
                        throw;

                    await Task.Delay(RetryDelayMilliseconds * attempt, cancellationToken);

                    if (_connection != null && _currentTransaction == null)
                    {
                        _connection.Dispose();
                        _connection = null;
                    }
                }
            }
        }

        private static bool IsTransientError(OracleException ex)
        {
            return ex.Number switch
            {
                1 => true,
                54 => true,
                60 => true,
                3113 => true,
                3114 => true,
                12170 => true,
                12571 => true,
                _ => false
            };
        }

        private static string MaskConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                return string.Empty;

            var parts = connectionString.Split(';');
            var masked = parts.Select(part =>
            {
                if (part.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
                    part.Contains("PWD", StringComparison.OrdinalIgnoreCase))
                {
                    return part.Split('=')[0] + "=****";
                }
                return part;
            });

            return string.Join(";", masked);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                if (_currentTransaction != null)
                {
                    try
                    {
                        _currentTransaction.Rollback();
                    }
                    catch
                    {
                    }
                    finally
                    {
                        _currentTransaction.Dispose();
                        _currentTransaction = null;
                    }
                }

                if (_connection != null)
                {
                    try
                    {
                        if (_connection.State == ConnectionState.Open)
                        {
                            _connection.Close();
                        }
                    }
                    catch
                    {
                    }
                    finally
                    {
                        _connection.Dispose();
                        _connection = null;
                    }
                }
            }

            _disposed = true;
        }

        ~OracleDbContext()
        {
            Dispose(false);
        }
    }
}
