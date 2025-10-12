using Interview1.Database.Common.Exceptions;
using Oracle.ManagedDataAccess.Client;

namespace Interview1.Database.Common
{
    public interface IDbContextTransaction : IDisposable
    {
        void Commit();
        Task CommitAsync(CancellationToken cancellationToken = default);
        void Rollback();
        Task RollbackAsync(CancellationToken cancellationToken = default);
    }

    public class OracleDbContextTransaction : IDbContextTransaction
    {
        private readonly OracleTransaction _transaction;
        private bool _disposed;
        private bool _completed;

        public OracleDbContextTransaction(OracleTransaction transaction)
        {
            _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        }

        public void Commit()
        {
            try
            {
                if (_completed)
                    throw new TransactionException("Transaction has already been completed.");

                _transaction.Commit();
                _completed = true;
            }
            catch (Exception ex) when (ex is not TransactionException)
            {
                throw new TransactionException("Failed to commit transaction.", ex);
            }
        }

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_completed)
                    throw new TransactionException("Transaction has already been completed.");

                await _transaction.CommitAsync(cancellationToken);
                _completed = true;
            }
            catch (Exception ex) when (ex is not TransactionException)
            {
                throw new TransactionException("Failed to commit transaction.", ex);
            }
        }

        public void Rollback()
        {
            try
            {
                if (_completed)
                    throw new TransactionException("Transaction has already been completed.");

                _transaction.Rollback();
                _completed = true;
            }
            catch (Exception ex) when (ex is not TransactionException)
            {
                throw new TransactionException("Failed to rollback transaction.", ex);
            }
        }

        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_completed)
                    throw new TransactionException("Transaction has already been completed.");

                await _transaction.RollbackAsync(cancellationToken);
                _completed = true;
            }
            catch (Exception ex) when (ex is not TransactionException)
            {
                throw new TransactionException("Failed to rollback transaction.", ex);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                if (!_completed)
                {
                    _transaction.Rollback();
                }
            }
            catch
            {
                // Suppress exceptions during disposal
            }
            finally
            {
                _transaction.Dispose();
                _disposed = true;
            }
        }
    }
}
