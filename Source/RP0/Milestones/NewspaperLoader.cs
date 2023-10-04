using System.IO;
using System.Reflection;
using UnityEngine;

namespace RP0
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class NewspaperLoader : MonoBehaviour
    {
        private static GameObject panelPrefab;
        public static GameObject PanelPrefab
        {
            get { return panelPrefab; }
        }

        private void Awake()
        {
            AssetBundle prefabs = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "newspaperui.dat"));
            panelPrefab = prefabs.LoadAsset("NewsBorder") as GameObject;
        }
    }
}
