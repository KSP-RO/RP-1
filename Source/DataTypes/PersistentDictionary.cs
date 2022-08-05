using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RP0.DataTypes
{
    public abstract class PersistentDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IConfigNode where TValue : IConfigNode
    {
        protected abstract TKey ParseKey(string key);

        public void Load(ConfigNode node)
        {
            Clear();
            foreach (ConfigNode n in node.nodes)
            {
                TKey key = ParseKey(n.name);
                TValue value = System.Activator.CreateInstance<TValue>();
                value.Load(n);
                Add(key, value);
            }
        }

        public void Save(ConfigNode node)
        {
            foreach (var kvp in this)
            {
                ConfigNode n = new ConfigNode(kvp.Key.ToString());
                kvp.Value.Save(n);
                node.AddNode(n);
            }
        }
    }

    public class PersistentDictionaryString<TValue> : PersistentDictionary<string, TValue> where TValue : IConfigNode
    {
        protected override string ParseKey(string key)
        {
            return key;
        }
    }

    /// <summary>
    /// NOTE: This does not have constraints because string is supported
    /// but string is not a valuetype
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class PersistentDictionaryValueTypes<TKey, TValue> : Dictionary<TKey, TValue>, IConfigNode
    {
        private static System.Reflection.MethodInfo ReadValueMethod = HarmonyLib.AccessTools.Method(typeof(ConfigNode), "ReadValue");
        private static System.Type KeyType = typeof(TKey);
        private static System.Type ValueType = typeof(TValue);

        public void Load(ConfigNode node)
        {
            Clear();
            foreach (ConfigNode.Value v in node.values)
            {
                TKey key = (TKey)ReadValueMethod.Invoke(null, new object[] { KeyType, v.name });
                TValue value = (TValue)ReadValueMethod.Invoke(null, new object[] { ValueType, v.value });
                Add(key, value);
            }
        }

        public void Save(ConfigNode node)
        {
            foreach (var kvp in this)
            {
                node.AddValue(kvp.Key.ToString(), kvp.Value.ToString());
            }
        }
    }
}
