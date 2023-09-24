using System;
using System.Collections.Generic;
using KSPCommunityFixes.Modding;

namespace RP0.DataTypes
{
    public class PersistentList<T> : List<T>, IConfigNode, ICloneable where T : IConfigNode
    {
        private static readonly Type _type = typeof(T);
        private static readonly string _typeName = typeof(T).Name;
        private static readonly Dictionary<string, Type> _typeCache = new Dictionary<string, Type>();

        public virtual void Load(ConfigNode node)
        {
            Clear();
            foreach (ConfigNode n in node.nodes)
            {
                T item;
                if (n.name == "ITEM" || n.name == _typeName)
                {
                    item = Activator.CreateInstance<T>();
                }
                else
                {
                    if (!_typeCache.TryGetValue(n.name, out var type))
                        type = HarmonyLib.AccessTools.TypeByName(n.name);
                    if (type == null || !_type.IsAssignableFrom(type))
                        type = _type;
                    else
                        _typeCache[n.name] = type;

                    item = (T)Activator.CreateInstance(type);
                }
                item.Load(n);
                Add(item);
            }
        }

        public void Save(ConfigNode node)
        {
            node.AddValue("version", 2);
            foreach (var item in this)
            {
                var type = item.GetType();
                ConfigNode n = new ConfigNode(type == _type ? _typeName : type.FullName);
                item.Save(n);
                node.AddNode(n);
            }
        }

        public virtual object Clone()
        {
            var clone = new PersistentList<T>();
            foreach (var v in this)
            {
                if (v is ICloneable c)
                {
                    clone.Add((T)c.Clone());
                }
                else
                {
                    ConfigNode n = new ConfigNode();
                    v.Save(n);
                    T item = (T)Activator.CreateInstance(v.GetType());
                    item.Load(n);
                    clone.Add(item);
                }
            }

            return clone;
        }
    }

    public class PersistentParsableList<T> : List<T>, IConfigNode where T : class
    {
        private enum ParseableType
        {
            INVALID,
            ProtoCrewMember,
        }

        private static ParseableType GetParseableType(Type t)
        {
            if (t == typeof(ProtoCrewMember))
                return ParseableType.ProtoCrewMember;

            return ParseableType.INVALID;
        }

        private static readonly ParseableType _ParseType = GetParseableType(typeof(T)); 

        private T Parse(string s)
        {
            switch(_ParseType)
            {
                case ParseableType.ProtoCrewMember:
                    return HighLogic.CurrentGame.CrewRoster[s] as T;
            }

            return null;
        }

        public void Load(ConfigNode node)
        {
            Clear();
            foreach (ConfigNode.Value v in node.values)
            {
                T item = Parse(v.value);
                if (item != null)
                    Add(item);
            }
        }

        public void Save(ConfigNode node)
        {
            foreach (var item in this)
            {
                node.AddValue("item", item.ToString());
            }
        }
    }

    /// <summary>
    /// NOTE: This does not have constraints because string is supported
    /// but string is not a valuetype
    /// </summary>
    public class PersistentListValueType<T> : List<T>, IConfigNode
    {
        private readonly static Type _Type = typeof(T);
        private readonly static DataType _DataType = FieldData.ValueDataType(_Type);

        public void Load(ConfigNode node)
        {
            Clear();
            foreach (ConfigNode.Value v in node.values)
            {
                T item = (T)FieldData.ReadValue(v.value, _DataType, _Type);
                Add(item);
            }
        }

        public void Save(ConfigNode node)
        {
            foreach (var item in this)
            {
                node.AddValue("item", FieldData.WriteValue(item, _DataType));
            }
        }
    }

    /// <summary>
    /// KCT Observable list - has callbacks for add/remove/update
    /// Derives from PersistentList
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class PersistentObservableList<T> : PersistentList<T> where T : IConfigNode
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

        public new int RemoveAll(Predicate<T> match)
        {
            int removed = 0;
            for (int i = Count - 1; i >= 0; --i)
            {
                T item = base[i];
                if (match(item))
                {
                    base.RemoveAt(i);
                    Removed(i, item);
                    ++removed;
                }
            }

            if (removed > 0)
                Updated();

            return removed;
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

        public override void Load(ConfigNode node)
        {
            base.Load(node);
            for (int i = 0; i < Count; ++i)
            {
                Added(i, base[i]);
            }
            Updated();
        }
    }
}
