using System;
using System.Collections.Generic;
using KSPCommunityFixes.Modding;

namespace RP0.DataTypes
{
    public class PersistentHashSet<T> : HashSet<T>, IConfigNode where T : IConfigNode
    {
        private static readonly Type _type = typeof(T);
        private static readonly string _typeName = typeof(T).Name;
        private static readonly Dictionary<string, Type> _typeCache = new Dictionary<string, Type>();

        public void Load(ConfigNode node)
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
    }

    /// <summary>
    /// NOTE: This does not have constraints because string is supported
    /// but string is not a valuetype
    /// </summary>
    public class PersistentHashSetValueType<T> : HashSet<T>, ICloneable, IConfigNode
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

        public object Clone()
        {
            var clone = new PersistentHashSetValueType<T>();
            foreach (var key in this)
                clone.Add(key);

            return clone;
        }
    }
}
