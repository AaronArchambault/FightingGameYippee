using System;
using System.Collections.Generic;
using UnityEngine;

namespace FightGame
{
    //this is a simple object pool so we are not making and destroying things every time someone gets hit
    //making new objects during a fight causes garbage collection spikes and in a fighting game even one dropped frame feels really bad
    public class ObjectPool<T> where T : Component
    {
        readonly Func<T> create;
        readonly Stack<T> free = new Stack<T>();
        readonly List<T> active = new List<T>();
        readonly int maxSize;

        public int ActiveCount { get { return active.Count; } }
        public List<T> Active { get { return active; } }

        //it makes some objects right away so the first hit of the match does not hitch
        public ObjectPool(Func<T> create, int prewarm, int maxSize = 256)
        {
            this.create = create;
            this.maxSize = maxSize;
            for (int i = 0; i < prewarm; i++)
            {
                var t = create();
                t.gameObject.SetActive(false);
                free.Push(t);
            }
        }

        //this gives you an object and if the pool is totally full it just steals one that is already out
        public T Get()
        {
            T t;
            if (free.Count > 0) t = free.Pop();
            else if (active.Count >= maxSize) { t = active[0]; active.RemoveAt(0); }
            else t = create();
            t.gameObject.SetActive(true);
            active.Add(t);
            return t;
        }

        public void Release(T t)
        {
            int i = active.IndexOf(t);
            if (i < 0) return;
            //it swaps with the last one so removing is fast
            int last = active.Count - 1;
            active[i] = active[last];
            active.RemoveAt(last);
            t.gameObject.SetActive(false);
            free.Push(t);
        }

        public void ReleaseAll()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                active[i].gameObject.SetActive(false);
                free.Push(active[i]);
            }
            active.Clear();
        }
    }
}
