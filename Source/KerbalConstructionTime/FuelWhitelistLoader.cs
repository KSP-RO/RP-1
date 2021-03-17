using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KerbalConstructionTime
{
    [KSPAddon(KSPAddon.Startup.Instantly, false)]
    public class GuiDataAndWhitelistItemsDatabase : MonoBehaviour
    {
        public static HashSet<string> ValidFuelRes = new HashSet<string>();
        public static HashSet<string> WasteRes = new HashSet<string>();

        private void Awake()
        {
            if (LoadingScreen.Instance?.loaders is List<LoadingSystem> loaders)
            {
                if (!(loaders.FirstOrDefault(x => x is FuelWhitelistLoader) is FuelWhitelistLoader))
                {
                    var go = new GameObject("KCTFuelWhitelistLoader");
                    var recipeLoader = go.AddComponent<FuelWhitelistLoader>();
                    loaders.Add(recipeLoader);
                }
            }
        }
    }

    public class FuelWhitelistLoader : LoadingSystem
    {
        private void LoadCustomItems()
        {
            foreach (var configNode in GameDatabase.Instance.GetConfigNodes("KCT_FUEL_RESOURCES"))
            {
                foreach (var item in configNode?.GetValuesList("fuelResource"))
                {
                    if (!string.IsNullOrEmpty(item))
                        GuiDataAndWhitelistItemsDatabase.ValidFuelRes.Add(item);
                }
                foreach (var item in configNode?.GetValuesList("wasteResource"))
                {
                    if (!string.IsNullOrEmpty(item))
                        GuiDataAndWhitelistItemsDatabase.WasteRes.Add(item);
                }
            }
        }

        public override bool IsReady() => LoadingScreen.Instance?.loaders != null;

        public override float ProgressFraction() => 0;

        public override string ProgressTitle() => "KerbalConstructionTime Initialization & Setup";

        public override void StartLoad()
        {
            LoadCustomItems();
        }
    }
}
