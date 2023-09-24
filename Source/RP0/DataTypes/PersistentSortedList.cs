using System;
using System.Collections.Generic;
using KSPCommunityFixes.Modding;

namespace RP0.DataTypes
{
    public abstract class PersistentSortedList<TKey, TValue> : SortedList<TKey, TValue>, IConfigNode where TValue : IConfigNode
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

    /// <summary>
    /// This does not have a struct constraint because string is not a valuetype but can be handled by ConfigNode's parser
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class PersistentSortedListValueTypeKey<TKey, TValue> : PersistentSortedList<TKey, TValue> where TValue : IConfigNode
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
    public class PersistentSortedListValueTypes<TKey, TValue> : SortedList<TKey, TValue>, IConfigNode
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

        public void Clone(PersistentSortedListValueTypes<TKey, TValue> source)
        {
            Clear();
            foreach (var kvp in source)
                Add(kvp.Key, kvp.Value);
        }

        public PersistentSortedListValueTypes<TKey, TValue> Clone()
        {
            var dict = new PersistentSortedListValueTypes<TKey, TValue>();
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
