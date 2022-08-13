using System;
using System.Collections.Generic;
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

    /// <summary>
    /// This does not have a struct constraint because string is not a valuetype but can be handled by ConfigNode's parser
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class PersistentDictionaryValueTypeKey<TKey, TValue> : PersistentDictionary<TKey, TValue> where TValue : IConfigNode
    {
        private static System.Reflection.MethodInfo ReadValueMethod = HarmonyLib.AccessTools.Method(typeof(ConfigNode), "ReadValue");
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
                key = (TKey)ReadValueMethod.Invoke(null, new object[] { KeyType, value });
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
                TKey key;
                if (KeyType == typeof(Guid))
                {
                    object o = new Guid(v.value);
                    key = (TKey)o;
                }
                else
                {
                    key = (TKey)ReadValueMethod.Invoke(null, new object[] { KeyType, v.name });
                }

                TValue value;
                if (ValueType == typeof(Guid))
                {
                    object o = new Guid(v.value);
                    value = (TValue)o;
                }
                else
                {
                    value = (TValue)ReadValueMethod.Invoke(null, new object[] { ValueType, v.value });
                }
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
