using Interview1.Database.Common;
using Interview1.Database.Common.Exceptions;

namespace Interview1.Database.Repositories
{
    public abstract class Repository
    {
        private readonly IDbContext _dbContext;

        protected Repository(IDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        protected async Task<IAsyncEnumerable<T>> Execute<T>(string name, List<OracleStoredProcParameter> parameters, CancellationToken cancellationToken = default) where T : IEntity, new()
        {
            async Task<IAsyncEnumerable<T>> action()
            {
                return await _dbContext.ExecuteStoredProcedureAsync<T>(name, parameters, cancellationToken);
            }

            var result = await Execute(action);

            return result is null ? throw new InvalidOperationException("Stored procedure execution returned null.") : result;
        }

        protected async Task Execute(string name, List<OracleStoredProcParameter> parameters, CancellationToken cancellationToken = default)
        {
            async Task action()
            {
                await _dbContext.ExecuteStoredProcedureAsync(name, parameters, cancellationToken);
            }

            await Execute(action);
        }

        private static async Task<IAsyncEnumerable<T>?> Execute<T>(Func<Task<IAsyncEnumerable<T>>> func)
        {
            try
            {
                return await func.Invoke();
            }
            catch (StoredProcedureException)
            {
                throw;
            }
            catch (DataMappingException)
            {
                throw;
            }
            catch (TransactionException)
            {
                throw;
            }
        }

        private static async Task Execute(Func<Task> func)
        {
            try
            {
                await func.Invoke();
            }
            catch (StoredProcedureException)
            {
                throw;
            }
            catch (DataMappingException)
            {
                throw;
            }
            catch (TransactionException)
            {
                throw;
            }
        }
    }
}
