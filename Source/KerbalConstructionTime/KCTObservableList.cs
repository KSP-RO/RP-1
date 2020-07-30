using System;
using System.Collections.Generic;

namespace KerbalConstructionTime
{
    public class KCTObservableList<T> : List<T>
    {
        public event Action Updated = delegate { };
        public event Action<int, T> Added = delegate (int idx, T element) { };
        public event Action<int, T> Removed = delegate (int idx, T element) { };

        public new void Add(T item)
        {
            base.Add(item);
            Added(Count - 1, item);
            Updated();
        }

        public new bool Remove(T item)
        {
            int idx = IndexOf(item);
            if (idx >= 0)
            {
                base.RemoveAt(idx);
                Removed(idx, item);
                Updated();
                return true;
            }
            return false;
        }

        public new void RemoveAt(int index)
        {
            T item = this[index];
            base.RemoveAt(index);
            Removed(index, item);
            Updated();
        }

        public new void AddRange(IEnumerable<T> collection)
        {
            foreach (T item in collection)
            {
                base.Add(item);
                Added(Count - 1, item);
            }
            Updated();
        }

        public new void RemoveRange(int index, int count)
        {
            for (int i = index + count - 1; i >= index; i--)
            {
                T el = this[i];
                base.RemoveAt(i);
                Removed(i, el);
            }
            Updated();
        }

        public new void Clear()
        {
            T[] arr = ToArray();
            base.Clear();
            for (int i = arr.Length - 1; i >= 0; i--)
            {
                Removed(i, arr[i]);
            }
            Updated();
        }

        public new void Insert(int index, T item)
        {
            base.Insert(index, item);
            Added(index, item);
            Updated();
        }

        public new void InsertRange(int index, IEnumerable<T> collection)
        {
            foreach (T item in collection)
            {
                base.Insert(index++, item);
                Added(index - 1, item);
            }
            Updated();
        }

        public new void RemoveAll(Predicate<T> match)
        {
            List<T> found = FindAll(match);
            foreach (T item in found)
            {
                Remove(item);
            }
            Updated();
        }

        public new T this[int index]
        {
            get
            {
                return base[index];
            }
            set
            {
                base[index] = value;
                Updated();
            }
        }
    }
}
