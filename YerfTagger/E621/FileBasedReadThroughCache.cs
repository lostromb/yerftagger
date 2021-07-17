using Durandal.Common.Cache;
using Durandal.Common.Collections;
using Durandal.Common.File;
using Durandal.Common.Logger;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YerfTagger.Cache;

namespace YerfTagger.E621
{
    public abstract class FileBasedReadThroughCache<K, V> : IAsyncReadThroughCache<K, V>
    {
        private readonly IFileSystem _fileSystem;
        private readonly VirtualPath _cacheFileName;
        private readonly Committer _fileCommitter;
        private readonly ReaderWriterLockAsync _lock = new ReaderWriterLockAsync(8);
        private readonly ILogger _logger;
        private readonly Dictionary<K, V> _memoryCache;
        private int _disposed = 0;

        public int CacheCapacity => int.MaxValue;

        public int ItemsCached
        {
            get;
            private set;
        }

        public FileBasedReadThroughCache(IFileSystem fileSystem, VirtualPath cacheFileName, ILogger logger)
        {
            _fileSystem = fileSystem;
            _logger = logger;
            _cacheFileName = cacheFileName;
            _memoryCache = new Dictionary<K, V>();
            _fileCommitter = new Committer(WriteCache, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30));
        }

        protected async Task InitializeCacheFile()
        {
            int hWrite = await _lock.EnterWriteLockAsync();
            try
            {
                _memoryCache.Clear();
                if (await _fileSystem.ExistsAsync(_cacheFileName))
                {
                    using (Stream rawStream = await _fileSystem.OpenStreamAsync(_cacheFileName, FileOpenMode.Open, FileAccessMode.Read))
                    {
                        await DeserializeCacheFile(rawStream, _memoryCache);
                    }

                    _logger.Log("Loaded " + _memoryCache.Count + " cache items from  " + _cacheFileName.FullName);
                }
                else
                {
                    _logger.Log("Cache file " + _cacheFileName.FullName + " does not exist", LogLevel.Wrn);
                }
            }
            finally
            {
                _lock.ExitWriteLock(hWrite);
            }
        }

        public void Clear()
        {
            _memoryCache.Clear();
            _fileCommitter.Commit();
        }

        public async Task<V> GetCache(K key)
        {
            int hRead = await _lock.EnterReadLockAsync();
            try
            {
                if (_memoryCache.ContainsKey(key))
                {
                    return _memoryCache[key];
                }
            }
            finally
            {
                _lock.ExitReadLock(hRead);
            }

            RetrieveResult<V> returnVal = await CacheMiss(key);
            if (returnVal != null && returnVal.Success)
            {
                int hWrite = await _lock.EnterWriteLockAsync();
                try
                {
                    // opt: this can potentially miss multiple times on the same cache key which is a bit wasteful.
                    _memoryCache[key] = returnVal.Result;
                }
                finally
                {
                    _lock.ExitWriteLock(hWrite);
                }

                _fileCommitter.Commit();
                return returnVal.Result;
            }
            else
            {
                return default(V);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 1)
            {
                return;
            }

            if (disposing)
            {
                _fileCommitter.WaitUntilCommitFinished(CancellationToken.None, DefaultRealTimeProvider.Singleton, 30000);
                _fileCommitter?.Dispose();
                _lock?.Dispose();
            }
        }

        protected abstract Task<RetrieveResult<V>> CacheMiss(K key);

        protected abstract Task DeserializeCacheFile(Stream cacheFileInStream, IDictionary<K, V> targetDictionary);

        protected abstract Task SerializeCacheFile(IDictionary<K, V> cachedItems, Stream cacheFileOutStream);

        private async Task WriteCache(IRealTimeProvider realTime)
        {
            int hRead = await _lock.EnterReadLockAsync();
            try
            {
                VirtualPath tempFile = _cacheFileName.Container.Combine(Guid.NewGuid().ToString("N") + ".tmp");
                using (Stream rawStream = await _fileSystem.OpenStreamAsync(tempFile, FileOpenMode.Create, FileAccessMode.Write))
                {
                    await SerializeCacheFile(_memoryCache, rawStream);
                }

                if (await _fileSystem.ExistsAsync(_cacheFileName))
                {
                    await _fileSystem.DeleteAsync(_cacheFileName);
                }

                await _fileSystem.MoveAsync(tempFile, _cacheFileName);
            }
            finally
            {
                _lock.ExitReadLock(hRead);
            }
        }
    }
}
