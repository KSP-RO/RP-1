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

        void IConfigNode.Load(ConfigNode node)
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

        void IConfigNode.Save(ConfigNode node)
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
}
