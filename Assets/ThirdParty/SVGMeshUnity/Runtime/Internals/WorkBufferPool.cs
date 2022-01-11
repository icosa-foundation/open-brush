using System;
using System.Collections.Generic;

namespace SVGMeshUnity.Internals
{
    public class WorkBufferPool
    {
        private readonly Dictionary<Type, Stack<object>> Pool = new Dictionary<Type, Stack<object>>();

        public WorkBuffer<T> Get<T>()
        {
            Stack<object> list;
            WorkBuffer<T> buf = null;

            if (Pool.TryGetValue(typeof(T), out list))
            {
                if (list.Count > 0)
                {
                    buf = (WorkBuffer<T>)list.Pop();
                }
            }
            
            return buf ?? new WorkBuffer<T>();
        }

        public void Get<T>(ref WorkBuffer<T> buf)
        {
            buf = Get<T>();
        }

        public void Release<T>(ref WorkBuffer<T> buf)
        {
            Stack<object> list;
            
            buf.Clear();

            if (!Pool.TryGetValue(typeof(T), out list))
            {
                Pool[typeof(T)] = list = new Stack<object>();
            }
            
            list.Push(buf);
            buf = null;
        }
    }
}