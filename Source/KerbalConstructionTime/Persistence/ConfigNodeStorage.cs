using System;
using UnityEngine;

namespace KerbalConstructionTime
{
    public abstract class ConfigNodeStorage : IPersistenceLoad, IPersistenceSave
    {
        //public abstract string ConfigNodeName { get; }

        public ConfigNodeStorage() { }

        public void PersistenceLoad()
        {
            OnDecodeFromConfigNode();
        }

        public void PersistenceSave()
        {
            OnEncodeToConfigNode();
        }

        public virtual void OnDecodeFromConfigNode() { }

        public virtual void OnEncodeToConfigNode() { }

        public ConfigNode AsConfigNode()
        {
            try
            {
                //Create a new Empty Node with the class name
                var cnTemp = new ConfigNode(GetType().Name);
                //Load the current object in there
                cnTemp = ConfigNode.CreateConfigFromObject(this, cnTemp);
                return cnTemp;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return new ConfigNode(GetType().Name);
            }
        }
    }
}

/*
    KerbalConstructionTime (c) by Michael Marvin, Zachary Eck

    KerbalConstructionTime is licensed under a
    Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.

    You should have received a copy of the license along with this
    work. If not, see <http://creativecommons.org/licenses/by-nc-sa/4.0/>.
*/
