using RP0.Crew;
using System;
using System.Collections;
using System.Collections.Generic;
using UniLinq;
using System.Reflection;
using UnityEngine;
using RealFuels;

namespace RP0
{
    [KSPAddon(KSPAddon.Startup.FlightEditorAndKSC, false)]
    public class ModuleShowInfoUpdater : MonoBehaviour
    {
        protected bool run = true;

        private void Awake()
        {
            if (HighLogic.LoadedSceneIsFlight)
                GameObject.Destroy(this);
        }

        private void Update()
        {
            if (run)
            {
                EntryCostDatabaseAccessor.Init();
                EntryCostDatabaseAccessor.GetFields();
                foreach (AvailablePart ap in EntryCostDatabaseAccessor.nameToPart.Values)
                {
                    if (ap.partPrefab.FindModuleImplementing<ModuleShowInfo>() is ModuleShowInfo msi)
                    {
                        foreach (AvailablePart.ModuleInfo x in ap.moduleInfos)
                        {
                            if (x.moduleName.Equals(ModuleShowInfo.sModuleName))
                            {
                                x.info = msi.GetInfo();
                            }
                        }
                    }
                }

                run = false;
                GameObject.Destroy(this);
            }
        }
    }

    public class EntryCostDatabaseAccessor
    {
        public static Dictionary<string, AvailablePart> nameToPart = null;
        private static FieldInfo nameToPartField = null;
        private static bool initialized = false;

        public static void Init()
        {
            if (initialized) return;
            initialized = true;

            nameToPartField = typeof(EntryCostDatabase).GetField("nameToPart", BindingFlags.NonPublic | BindingFlags.Static);
        }

        public static void GetFields()
        {
            nameToPart = (Dictionary<string, AvailablePart>)nameToPartField.GetValue(null);
        }

        public static int GetCost(PartEntryCostHolder h, bool clearTracker=true)
        {
            if (h == null)
                return 0;
            if (clearTracker) EntryCostDatabase.ClearTracker();
            return h.GetCost();
        }

        private static string CostString(PartEntryCostHolder h, int cost=-1)
        {
            if (h == null)
                return string.Empty;

            if (cost == -1) cost = GetCost(h);
            string ret = h.name;
            if (EntryCostDatabase.IsUnlocked(h.name))
                ret = $"<color=green>{ret}</color>"; 
            else
                ret += $": {cost}";

            return ret;
        }

        public static string DisplayHolder(RealFuels.PartEntryCostHolder h, bool recurse=false)
        {
            if (h is null) return "null";
            if (h.cost == 0 && h.children.Count == 1) return DisplayHolder(EntryCostDatabase.GetHolder(h.children[0]), recurse);
            string s = string.Empty;
            foreach (string child in h.children)
            {
                s += $" | {CostString(EntryCostDatabase.GetHolder(child))}";
            }
            // Append the recursive scan after building the top level, instead of during.
            if (recurse)
            {
                foreach (string child in h.children)
                {
                    s += $"[{DisplayHolder(EntryCostDatabase.GetHolder(child), recurse)}]";
                }
            }

            return $"{CostString(h, h.cost)}{s}";
        }
    }

    public class ModuleShowInfo : PartModule
    {
        public const string sModuleName = "Show Info";
        public const string dragCubeGroup = "Drag Cubes";

        [KSPField(guiName = "X+", groupName = dragCubeGroup, groupDisplayName = dragCubeGroup)] private string XP;
        [KSPField(guiName = "X-", groupName = dragCubeGroup)] private string XN;
        [KSPField(guiName = "Y+", groupName = dragCubeGroup)] private string YP;
        [KSPField(guiName = "Y-", groupName = dragCubeGroup)] private string YN;
        [KSPField(guiName = "Z+", groupName = dragCubeGroup)] private string ZP;
        [KSPField(guiName = "Z-", groupName = dragCubeGroup)] private string ZN;
        [KSPField] public float updateInterval = 0.25f;

        private bool _showCubeInfo = false;
        private WaitForSeconds _waitInstruction;

        public override string GetModuleDisplayName() => "Unlock Requirements";

        public override string GetInfo()
        {
            EntryCostDatabaseAccessor.Init();
            EntryCostDatabaseAccessor.GetFields();

            string data = null, apInfo = null;
            string nm = RealFuels.Utilities.SanitizeName(part.name);
            if (HighLogic.LoadedScene != GameScenes.LOADING && EntryCostDatabase.GetHolder(nm) is PartEntryCostHolder h)
                data = $"Total cost: {EntryCostDatabaseAccessor.GetCost(h)}\n{EntryCostDatabaseAccessor.DisplayHolder(h, false)}";
            if (part.partInfo is AvailablePart ap)
            {
                apInfo = $"Tech Required: {ap.TechRequired}";
                if (part.CrewCapacity > 0)
                    apInfo = $"Training course: {(TrainingDatabase.SynonymReplace(part.name, out string name) ? name : ap.title)}\n{apInfo}";
            }
            string res = $"Part name: {part.name}\n{apInfo}\n{data}";
            return res;
        }

        public override void OnStart(StartState state)
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                _waitInstruction = new WaitForSeconds(updateInterval);
                StartCoroutine(DragCubeDisplay());
            }
        }

        private IEnumerator DragCubeDisplay()
        {
            while (HighLogic.LoadedSceneIsFlight)
            {
                if (_showCubeInfo != PhysicsGlobals.AeroDataDisplay)
                {
                    _showCubeInfo = PhysicsGlobals.AeroDataDisplay;
                    Fields[nameof(XP)].guiActive = _showCubeInfo;
                    Fields[nameof(XN)].guiActive = _showCubeInfo;
                    Fields[nameof(YP)].guiActive = _showCubeInfo;
                    Fields[nameof(YN)].guiActive = _showCubeInfo;
                    Fields[nameof(ZP)].guiActive = _showCubeInfo;
                    Fields[nameof(ZN)].guiActive = _showCubeInfo;
                }
                if (_showCubeInfo)
                    BuildCubeData();
                yield return _waitInstruction;
            }
        }

        private void BuildCubeData()
        {
            DragCubeList cubes = part.DragCubes;
            XP = $"{cubes.WeightedArea[0]:F2} ({cubes.GetCubeAreaDir(DragCubeList.GetFaceDirection(DragCube.DragFace.XP)):F2})";
            XN = $"{cubes.WeightedArea[1]:F2} ({cubes.GetCubeAreaDir(DragCubeList.GetFaceDirection(DragCube.DragFace.XN)):F2})";
            YP = $"{cubes.WeightedArea[2]:F2} ({cubes.GetCubeAreaDir(DragCubeList.GetFaceDirection(DragCube.DragFace.YP)):F2})";
            YN = $"{cubes.WeightedArea[3]:F2} ({cubes.GetCubeAreaDir(DragCubeList.GetFaceDirection(DragCube.DragFace.YN)):F2})";
            ZP = $"{cubes.WeightedArea[4]:F2} ({cubes.GetCubeAreaDir(DragCubeList.GetFaceDirection(DragCube.DragFace.ZP)):F2})";
            ZN = $"{cubes.WeightedArea[5]:F2} ({cubes.GetCubeAreaDir(DragCubeList.GetFaceDirection(DragCube.DragFace.ZN)):F2})";
        }
    }
}
