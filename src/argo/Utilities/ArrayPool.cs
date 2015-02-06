using System;
using System.Threading;

namespace Utilities
{
    internal class ArrayPool<T>
    {
        private ObjectPool<T[]>[] pools;

        public ArrayPool(int maxArrayLength = 32)
        {
            this.pools = new ObjectPool<T[]>[maxArrayLength];
        }

        public T[] AllocateArrayFromPool(int size)
        {
            var pool = GetPool(size);
            return pool.AllocateFromPool();
        }

        public void ReturnToPool(T[] array)
        {
            var pool = GetPool(array.Length);
            pool.ReturnToPool(array);
        }

        private ObjectPool<T[]> GetPool(int size)
        {
            if (size >= this.pools.Length)
            {
                throw new ArgumentOutOfRangeException("size");
            }

            var pool = this.pools[size];
            if (pool == null)
            {
                var newPool = new ObjectPool<T[]>(() => new T[size], array => Array.Clear(array, 0, size));
                Interlocked.CompareExchange(ref this.pools[size], newPool, null);
                pool = this.pools[size];
            }

            return pool;
        }
    }
}