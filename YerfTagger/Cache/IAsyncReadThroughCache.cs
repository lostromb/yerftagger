using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YerfTagger.Cache
{
    public interface IAsyncReadThroughCache<K, V> : IDisposable
    {
        Task<V> GetCache(K key);

        void Clear();
    }
}
