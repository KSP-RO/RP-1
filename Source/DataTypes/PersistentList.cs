using System;
using System.Collections.Generic;
using UniLinq;
using System.Text;
using System.Threading.Tasks;

namespace RP0.DataTypes
{
    public class PersistentList<T> : List<T>, IConfigNode where T : IConfigNode
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

    public class PersistentParsableList<T> : List<T>, IConfigNode where T : class
    {
        protected virtual T Parse(string s)
        {
            if (typeof(T) == typeof(ProtoCrewMember))
            {
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
        private static System.Reflection.MethodInfo ReadValueMethod = HarmonyLib.AccessTools.Method(typeof(ConfigNode), "ReadValue");
        private static System.Type type = typeof(T);

        public void Load(ConfigNode node)
        {
            Clear();
            foreach (ConfigNode.Value v in node.values)
            {
                T item;
                if (type == typeof(Guid))
                {
                    object o = new Guid(v.value);
                    item = (T)o;
                }
                else
                {
                    item = (T)ReadValueMethod.Invoke(null, new object[] { type, v.value });
                }
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
}
