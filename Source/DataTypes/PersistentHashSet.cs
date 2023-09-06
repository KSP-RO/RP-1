using System;
using System.Collections.Generic;
using KSPCommunityFixes.Modding;

namespace RP0.DataTypes
{
    public class PersistentHashSet<T> : HashSet<T>, IConfigNode where T : IConfigNode
    {
        private static string typeName = typeof(T).Name;

        public void Load(ConfigNode node)
        {
            Clear();
            foreach (ConfigNode n in node.nodes)
            {
                T item = System.Activator.CreateInstance<T>();
                item.Load(n);
                Add(item);
            }
        }

        public void Save(ConfigNode node)
        {
            foreach (var item in this)
            {
                ConfigNode n = new ConfigNode(typeName);
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
        private readonly static System.Type _Type = typeof(T);
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
