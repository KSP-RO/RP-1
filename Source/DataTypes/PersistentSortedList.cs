using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace RP0.DataTypes
{
    public abstract class PersistentSortedList<TKey, TValue> : SortedList<TKey, TValue>, IConfigNode where TValue : IConfigNode
    {
        protected abstract TKey ParseKey(string key);

        public void Load(ConfigNode node)
        {
            Clear();
            if (node.values.Count == 0 || node.nodes.Count == 0 || node.values[0].name != "version")
            {
                foreach (ConfigNode n in node.nodes)
                {
                    TKey key = ParseKey(n.name);
                    TValue value = System.Activator.CreateInstance<TValue>();
                    value.Load(n);
                    Add(key, value);
                }
                return;
            }

            ConfigNode keyNode = node.nodes[0];
            ConfigNode valueNode = node.nodes[1];
            for (int i = 0; i < keyNode.values.Count; ++i)
            {
                TKey key = ParseKey(keyNode.values[i].value);
                TValue value = System.Activator.CreateInstance<TValue>();
                value.Load(valueNode.nodes[i]);
                Add(key, value);
            }
        }

        public void Save(ConfigNode node)
        {
            node.AddValue("version", 1);
            ConfigNode keyNode = node.AddNode("Keys");
            ConfigNode valueNode = node.AddNode("Values");

            foreach (var kvp in this)
            {
                keyNode.AddValue("key", kvp.Key.ToString());
                ConfigNode n = new ConfigNode("VALUE");
                kvp.Value.Save(n);
                valueNode.AddNode(n);
            }
        }
    }

    /// <summary>
    /// This does not have a struct constraint because string is not a valuetype but can be handled by ConfigNode's parser
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class PersistentSortedListValueTypeKey<TKey, TValue> : PersistentSortedList<TKey, TValue> where TValue : IConfigNode
    {
        private static System.Type KeyType = typeof(TKey);

        protected override TKey ParseKey(string value)
        {
            TKey key;
            if (KeyType == typeof(Guid))
            {
                object o = new Guid(value);
                key = (TKey)o;
            }
            else
            {
                key = (TKey)ConfigNode.ReadValue(KeyType, value);
            }
            return key;
        }
    }

    /// <summary>
    /// NOTE: This does not have constraints because string is supported
    /// but string is not a valuetype
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class PersistentSortedListValueTypes<TKey, TValue> : SortedList<TKey, TValue>, IConfigNode
    {
        private static Type KeyType = typeof(TKey);
        private static Type ValueType = typeof(TValue);

        public void Load(ConfigNode node)
        {
            Clear();
            if (node.values.Count == 0 || node.nodes.Count == 0 || node.values[0].name != "version")
            {
                foreach (ConfigNode.Value v in node.values)
                {
                    TKey key;
                    if (KeyType == typeof(Guid))
                    {
                        object o = new Guid(v.value);
                        key = (TKey)o;
                    }
                    else
                    {
                        key = (TKey)ConfigNode.ReadValue(KeyType, v.name);
                    }

                    TValue value;
                    if (ValueType == typeof(Guid))
                    {
                        object o = new Guid(v.value);
                        value = (TValue)o;
                    }
                    else
                    {
                        value = (TValue)ConfigNode.ReadValue(ValueType, v.value);
                    }
                    Add(key, value);
                }
                return;
            }

            ConfigNode keyNode = node.nodes[0];
            ConfigNode valueNode = node.nodes[1];
            for (int i = 0; i < keyNode.values.Count; ++i)
            {
                TKey key;
                if (KeyType == typeof(Guid))
                {
                    object o = new Guid(keyNode.values[i].value);
                    key = (TKey)o;
                }
                else
                {
                    key = (TKey)ConfigNode.ReadValue(KeyType, keyNode.values[i].value);
                }

                TValue value;
                if (ValueType == typeof(Guid))
                {
                    object o = new Guid(valueNode.values[i].value);
                    value = (TValue)o;
                }
                else
                {
                    value = (TValue)ConfigNode.ReadValue(ValueType, valueNode.values[i].value);
                }

                Add(key, value);
            }
        }

        public void Save(ConfigNode node)
        {
            node.AddValue("version", 1);
            ConfigNode keyNode = node.AddNode("Keys");
            ConfigNode valueNode = node.AddNode("Values");

            foreach (var kvp in this)
            {
                keyNode.AddValue("key", kvp.Key.ToString());
                valueNode.AddValue("value", kvp.Value.ToString());
            }
        }

        public void Clone(PersistentDictionaryValueTypes<TKey, TValue> source)
        {
            Clear();
            foreach (var kvp in source)
                Add(kvp.Key, kvp.Value);
        }

        public PersistentDictionaryValueTypes<TKey, TValue> Clone()
        {
            var dict = new PersistentDictionaryValueTypes<TKey, TValue>();
            foreach (var kvp in this)
                dict.Add(kvp.Key, kvp.Value);

            return dict;
        }

        public static bool AreEqual(Dictionary<TKey, TValue> d1, Dictionary<TKey, TValue> d2)
        {
            if (d1.Count != d2.Count) return false;
            foreach (TKey key in d1.Keys)
                if (!d2.TryGetValue(key, out TValue val) || !val.Equals(d1[key]))
                    return false;

            return true;
        }
    }
}
