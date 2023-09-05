using System;
using System.Collections.Generic;
using UniLinq;
using System.Text;
using System.Threading.Tasks;

namespace RP0.DataTypes
{
    public class Boxed<T> where T : struct
    {
        public T value;
        public Boxed(T val) { value = val; }
        public Boxed() { }
    }

    public abstract class ConfigNodePersistenceBase : IConfigNode
    {
        public virtual void Load(ConfigNode node)
        {
            ConfigNode.LoadObjectFromConfig(this, node);
        }

        public virtual void Save(ConfigNode node)
        {
            ConfigNode.CreateConfigFromObject(this, node);
        }
    }
}
