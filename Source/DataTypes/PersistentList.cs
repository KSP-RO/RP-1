using System;
using System.Collections.Generic;
using KSPCommunityFixes.Modding;

namespace RP0.DataTypes
{
    public class PersistentList<T> : List<T>, IConfigNode, ICloneable where T : IConfigNode
    {
        private static string typeName = typeof(T).Name;

        public virtual void Load(ConfigNode node)
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
                    T item = System.Activator.CreateInstance<T>();
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

        private static ParseableType GetParseableType(System.Type t)
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
    }
}
