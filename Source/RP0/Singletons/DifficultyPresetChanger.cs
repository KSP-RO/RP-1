using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using ROUtils;

namespace RP0
{
    public class DifficultyPresetChanger : HostedSingleton
    {
        public DifficultyPresetChanger(SingletonHost host) : base(host) { }

        public override void Awake()
        {
            ConfigNode paramsNode = null;
            foreach (ConfigNode n in GameDatabase.Instance.GetConfigNodes("GAMEPARAMETERS"))
                paramsNode = n;

            if (paramsNode == null)
            {
                RP0Debug.LogError("Could not find GAMEPARAMETERS node.");
                return;
            }

            GameParameters.SetDifficultyPresets();

            foreach (KeyValuePair<GameParameters.Preset, GameParameters> kvp in GameParameters.DifficultyPresets)
            {
                ConfigNode n = paramsNode.GetNode(kvp.Key.ToString());
                if (n != null)
                    kvp.Value.Load(n);
            }

            RP0Debug.Log("Reset difficulty presets.");
        }
    }
}
