using UnityEngine;
using KSP.Localization;
using System.Collections.Generic;
using RP0.ProceduralAvionics;

namespace RP0
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class LocalizationHandler : MonoBehaviour
    {
        public void Awake()
        {
            DontDestroyOnLoad(this);
            GameEvents.onLanguageSwitched.Add(OnLanguageChange);
            OnLanguageChange(); // Initialize
        }

        private void OnLanguageChange()
        {
            foreach (PartUpgradeHandler.Upgrade up in PartUpgradeManager.Handler)
            {
                // Proc avionics upgrades
                if (up.name.StartsWith("procAvionics-tltech"))
                {
                    string desc = BuildAvionicsStats(up.techRequired);
                    if (!string.IsNullOrEmpty(desc))
                        up.description = Localizer.GetStringByTag("#rp0AvionicsUpgradeText") + desc;

                    continue;
                }
            }
        }

        
        private string BuildAvionicsStats(string techRequired)
        {
            string retStr = string.Empty;
            foreach (var avConfig in ProceduralAvionicsTechManager.AllAvionicsConfigs)
            {
                for (int i = 1; i < avConfig.TechNodesSorted.Length; ++i)
                {
                    ProceduralAvionicsTechNode node = avConfig.TechNodesSorted[i];
                    if (node.TechNodeName == techRequired)
                    {
                        ModuleProceduralAvionics.GetStatsForTechNode(avConfig.TechNodesSorted[i - 1], -1f, out float massOld, out float costOld, out float powerOld);
                        ModuleProceduralAvionics.GetStatsForTechNode(node, -1f, out float massNew, out float costNew, out float powerNew);

                        retStr += "\n" + Localizer.Format("#rp0AvionicsUpgradeTextLine",
                            Localizer.GetStringByTag("#rp0AvionicsType_" + avConfig.name),
                            FormatRatioAsPercent(massNew / massOld),
                            FormatRatioAsPercent(costNew / costOld),
                            FormatRatioAsPercent(powerNew / powerOld),
                            FormatBytes(node.kosDiskSpace));

                        break;
                    }
                }
            }

            return retStr;
        }

        private string FormatRatioAsPercent(double ratio)
        {
            if (ratio < 1d)
                return Localizer.Format("#rp0NegativePercent", ((1d - ratio) * 100d).ToString("N0"));

            return Localizer.Format("#rp0PositivePercent", ((ratio - 1d) * 100d).ToString("N0"));
        }

        private string FormatBytes(int amount)
        {
            if (amount < 1024)
                return Localizer.Format("#rp0DiskSpaceB", amount);
            if(amount < 1024*1024)
                return Localizer.Format("#rp0DiskSpacekB", (amount / 1024d).ToString("0.#"));

            return Localizer.Format("#rp0DiskSpaceMB", (amount / (1024d * 1024d)).ToString("0.#"));
        }
    }
}
