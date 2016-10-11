using System;
using System.Linq;
using System.Threading;
using Vertica.Integration.Infrastructure.Database;
using Vertica.Integration.Infrastructure.Features;

namespace Vertica.Integration.Infrastructure.Threading.DistributedMutex.Db
{
    internal class DbDistributedMutex : IDistributedMutex
    {
        private readonly IDbFactory _db;
        private readonly IFeatureToggler _featureToggler;
        private readonly IShutdown _shutdown;

        private readonly TimeSpan _queryLockInterval;

        public static readonly string QueryLockIntervalKey = $"{nameof(DbDistributedMutex)}.QueryLockInterval";

        public DbDistributedMutex(IDbFactory db, IRuntimeSettings settings, IFeatureToggler featureToggler, IShutdown shutdown)
        {
            _db = db;
            _featureToggler = featureToggler;
            _shutdown = shutdown;

            TimeSpan queryLockInterval;
            if (!TimeSpan.TryParse(settings[QueryLockIntervalKey], out queryLockInterval))
                queryLockInterval = TimeSpan.FromSeconds(5);

            _queryLockInterval = queryLockInterval;
        }

        public IDisposable Enter(DistributedMutexContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            if (_featureToggler.IsDisabled<DbDistributedMutex>())
                return null;

            return new EnterLock(_db, context, _queryLockInterval, _shutdown.Token);
        }

        private class EnterLock : IDisposable
        {
            private readonly IDbFactory _db;
            private readonly DbDistributedMutexLock _newLock;

            private CancellationTokenRegistration _onCancel;
            private readonly object _releaseOnce = new object();
            private bool _isReleased;

            public EnterLock(IDbFactory db, DistributedMutexContext context, TimeSpan queryLockInterval, CancellationToken cancellationToken)
            {
                _db = db;
                _newLock = new DbDistributedMutexLock(context.Name)
                {
                    MachineName = Environment.MachineName
                };

                using (IDbSession session = db.OpenSession())
                {
                    TimeSpan waitTime = context.Configuration.WaitTime;
                    int maxRetries = Math.Max((int)Math.Ceiling(waitTime.TotalMilliseconds / queryLockInterval.TotalMilliseconds), 1);
                    int attempts = 0;

                    DbDistributedMutexLock currentLock;

                    while (true)
                    {
                        if (TryEnterLock(session, out currentLock))
                        {
                            _onCancel = cancellationToken.Register(ReleaseLock);
                            return;
                        }

                        if (currentLock == null)
                            throw new InvalidOperationException($"Cannot obtain lock for '{_newLock.Name}', but current lock is null.");

                        if (++attempts >= maxRetries)
                            break;

                        context.Waiting($"{currentLock}. Waiting for {queryLockInterval} (attemt {attempts} of {maxRetries}).");

                        cancellationToken.WaitHandle.WaitOne(queryLockInterval);
                    }

                    throw new DistributedMutexTimeoutException($"Unable to acquire lock '{_newLock.Name}' within wait time ({waitTime}) using {attempts} attempts with a query interval of {queryLockInterval}. {currentLock}");
                }
            }

            private bool TryEnterLock(IDbSession session, out DbDistributedMutexLock currentLock)
            {
                currentLock = session.Query<DbDistributedMutexLock>(@"
BEGIN TRY
    INSERT INTO DistributedMutex (Name, LockId, CreatedAt, MachineName)
        VALUES (@Name, @LockId, @CreatedAt, @MachineName)
END TRY

BEGIN CATCH
    DECLARE @ErrorNumber AS INT
    SELECT @ErrorNumber = ERROR_NUMBER();

    IF @ErrorNumber = 2627 -- PRIMARY KEY VIOLATION
	    SELECT * FROM DistributedMutex WHERE (Name = @Name);
    ELSE
	    THROW
END CATCH
", _newLock).FirstOrDefault();

                return currentLock == null;
            }

            private void ReleaseLock()
            {
                if (!_isReleased)
                {
                    lock (_releaseOnce)
                    {
                        if (!_isReleased)
                        {
                            using (IDbSession session = _db.OpenSession())
                            {
                                session.Execute(@"DELETE FROM DistributedMutex WHERE (Name = @Name AND LockId = @LockId)", _newLock);
                            }

                            _isReleased = true;
                        }
                    }
                }
            }

            public void Dispose()
            {
                ReleaseLock();

                _onCancel.Dispose();
            }
        }
    }
}