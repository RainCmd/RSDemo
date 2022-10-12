using System.Collections.Generic;

namespace NameSpace
{
    public class LRPipeline<T>
    {
        private readonly Queue<T> cache = new Queue<T>();
        private readonly T[] datas = new T[8192];
        private uint head, tail;
        public void En(T data)
        {
            cache.Enqueue(data);
            while (head - tail < 8192 && cache.Count > 0)
            {
                datas[head & 8191] = cache.Dequeue();
                head++;
            }
        }
        public bool TryDe(out T data)
        {
            if (head != tail)
            {
                data = datas[tail & 8191];
                tail++;
                return true;
            }
            data = default;
            return false;
        }
        public int Count { get { return (int)(head - tail) + cache.Count; } }
    }
}