using System.IO;
using System.Linq;

namespace KerbalConstructionTime
{
    public class KCTSettings
    {
        private const string _fileName = "KCT_Config.txt";
        private readonly string _directory = KSPUtil.ApplicationRootPath + "GameData/RP-0/PluginData/";

        [Persistent]
        public int MaxTimeWarp;
        [Persistent]
        public bool ForceStopWarp;
        [Persistent]
        public bool DisableAllMessages;
        [Persistent]
        public bool AutoKACAlarms;
        [Persistent]
        public bool Debug;
        [Persistent]
        public bool OverrideLaunchButton;
        [Persistent]
        public bool PreferBlizzyToolbar;
        [Persistent]
        public bool RandomizeCrew;
        [Persistent]
        public int WindowMode = 1;

        public KCTSettings()
        {
            MaxTimeWarp = TimeWarp.fetch.warpRates.Count() - 1;
            ForceStopWarp = false;
            DisableAllMessages = false;
            Debug = false;
            OverrideLaunchButton = true;
            AutoKACAlarms = false;
            PreferBlizzyToolbar = false;
        }

        public void Load()
        {
            if (File.Exists(Path.Combine(_directory, _fileName)))
            {
                ConfigNode cnToLoad = ConfigNode.Load(Path.Combine(_directory, _fileName));
                ConfigNode.LoadObjectFromConfig(this, cnToLoad);

                KCT_GUI.AssignRandomCrew = RandomizeCrew;
            }
        }

        public void Save()
        {
            Directory.CreateDirectory(_directory);
            ConfigNode cnTemp = ConfigNode.CreateConfigFromObject(this, new ConfigNode());
            cnTemp.Save(Path.Combine(_directory, _fileName));
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
