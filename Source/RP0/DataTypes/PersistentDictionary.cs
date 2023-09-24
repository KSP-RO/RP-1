using System;
using System.Collections.Generic;
using KSPCommunityFixes.Modding;

namespace RP0.DataTypes
{
    public abstract class PersistentDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IConfigNode where TValue : IConfigNode
    {
        private static readonly Type _type = typeof(TValue);
        private static readonly string _typeName = typeof(TValue).Name;
        private static readonly Dictionary<string, Type> _typeCache = new Dictionary<string, Type>();

        protected abstract TKey ParseKey(string key);

        protected abstract string WriteKey(TKey key);

        public void Load(ConfigNode node)
        {
            
            Clear();
            ConfigNode keyNode = node.nodes[0];
            ConfigNode valueNode = node.nodes[1];
            for (int i = 0; i < keyNode.values.Count; ++i)
            {
                TKey key = ParseKey(keyNode.values[i].value);

                var n = valueNode.nodes[i];
                TValue value;
                if (n.name == "VALUE" || n.name == _typeName)
                {
                    value = Activator.CreateInstance<TValue>();
                }
                else
                {
                    if (!_typeCache.TryGetValue(n.name, out var type))
                        type = HarmonyLib.AccessTools.TypeByName(n.name);
                    if (type == null || !_type.IsAssignableFrom(type))
                        type = _type;
                    else
                        _typeCache[n.name] = type;

                    value = (TValue)Activator.CreateInstance(type);
                }
                value.Load(n);
                Add(key, value);
            }
        }

        public void Save(ConfigNode node)
        {
            node.AddValue("version", 2);
            ConfigNode keyNode = node.AddNode("Keys");
            ConfigNode valueNode = node.AddNode("Values");

            foreach (var kvp in this)
            {
                keyNode.AddValue("key", WriteKey(kvp.Key));
                var type = kvp.Value.GetType();
                ConfigNode n = new ConfigNode(type == _type ? _typeName : type.FullName);
                kvp.Value.Save(n);
                valueNode.AddNode(n);
            }
        }
    }

    public class PersistentDictionaryNodeKeyed<TValue> : Dictionary<string, TValue>, IConfigNode where TValue : IConfigNode
    {
        private static readonly Type _type = typeof(TValue);
        private static readonly string _typeName = typeof(TValue).Name;
        private static readonly Dictionary<string, Type> _typeCache = new Dictionary<string, Type>();

        private string _keyName = "name";

        public PersistentDictionaryNodeKeyed() {}
        
        public PersistentDictionaryNodeKeyed(string keyName)
        {
            _keyName = keyName;
        }

        public void Load(ConfigNode node)
        {
            Clear();
            for (int i = 0; i < node.nodes.Count; ++i)
            {
                var n = node.nodes[i];
                string key = n.GetValue(_keyName);
                if (string.IsNullOrEmpty(key))
                {
                    RP0Debug.LogError("PersistentDictionaryNodeKeyed: null or empty key in node! Skipping. Node=\n" + n.ToString());
                    continue;
                }

                TValue value;
                if (n.name == "VALUE" || n.name == _typeName)
                {
                    value = Activator.CreateInstance<TValue>();
                }
                else
                {
                    if (!_typeCache.TryGetValue(n.name, out var type))
                        type = HarmonyLib.AccessTools.TypeByName(n.name);
                    if (type == null || !_type.IsAssignableFrom(type))
                        type = _type;
                    else
                        _typeCache[n.name] = type;

                    value = (TValue)Activator.CreateInstance(type);
                }
                value.Load(n);
                Add(key, value);
            }
        }

        public void Save(ConfigNode node)
        {
            node.AddValue("version", 2);
            foreach (var kvp in this)
            {
                var type = kvp.Value.GetType();
                ConfigNode n = new ConfigNode(type == _type ? _typeName : type.FullName);
                kvp.Value.Save(n);
                n.SetValue(_keyName, kvp.Key, true);
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
        private static readonly Type _KeyType = typeof(TKey);
        private static readonly DataType _KeyDataType = FieldData.ValueDataType(_KeyType);

        protected override TKey ParseKey(string value)
        {
            return (TKey)FieldData.ReadValue(value, _KeyDataType, _KeyType);
        }

        protected override string WriteKey(TKey key)
        {
            return FieldData.WriteValue(key, _KeyDataType);
        }
    }

    /// <summary>
    /// NOTE: This does not have constraints because string is supported
    /// but string is not a valuetype
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class PersistentDictionaryValueTypes<TKey, TValue> : Dictionary<TKey, TValue>, ICloneable, IConfigNode
    {
        private static Type _KeyType = typeof(TKey);
        private static readonly DataType _KeyDataType = FieldData.ValueDataType(_KeyType);
        private static Type _ValueType = typeof(TValue);
        private static readonly DataType _ValueDataType = FieldData.ValueDataType(_ValueType);

        public void Load(ConfigNode node)
        {
            Clear();
            foreach (ConfigNode.Value v in node.values)
            {
                TKey key = (TKey)FieldData.ReadValue(v.name, _KeyDataType, _KeyType);
                TValue value = (TValue)FieldData.ReadValue(v.value, _ValueDataType, _ValueType);
                if (ContainsKey(key))
                {
                    RP0Debug.LogError($"PersistentDictionary: Contains key {key}");
                    Remove(key);
                }
                Add(key, value);
            }
        }

        public void Save(ConfigNode node)
        {
            foreach (var kvp in this)
            {

                string key = FieldData.WriteValue(kvp.Key, _KeyDataType);
                string value = FieldData.WriteValue(kvp.Value, _ValueDataType);
                node.AddValue(key, value);
            }
        }

        public void Clone(PersistentDictionaryValueTypes<TKey, TValue> source)
        {
            Clear();
            foreach (var kvp in source)
                Add(kvp.Key, kvp.Value);
        }

        public object Clone()
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
