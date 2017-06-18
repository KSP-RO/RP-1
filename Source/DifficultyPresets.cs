using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;

namespace RP0
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    class DifficultyPresetChanger : MonoBehaviour
    {
        public void Awake()
        {
            ConfigNode paramsNode = GameDatabase.Instance.GetConfigNode("GAMEPARAMETERS");
            if (paramsNode == null)
                return;

            foreach (ConfigNode n in paramsNode.nodes)
            {
                try
                {
                    GameParameters.Preset preset = (GameParameters.Preset)Enum.Parse(typeof(GameParameters.Preset), n.name);

                    GameParameters p;
                    if (GameParameters.DifficultyPresets.TryGetValue(preset, out p))
                    {
                        p.Load(n);
                    }
                }
                catch { }
            }
        }
    }
}
