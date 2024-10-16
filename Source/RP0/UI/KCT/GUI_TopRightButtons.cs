using System.Collections.Generic;
using UniLinq;
using UnityEngine;

namespace RP0
{
    [KSPAddon(KSPAddon.Startup.FlightEditorAndKSC, false)]
    internal class GUI_TopRightButtonSection : MonoBehaviour
    {
        private float _uiScale;

        private readonly List<GUI_TopRightButton> _buttons = new List<GUI_TopRightButton>();

        private static bool? _isMechJebInstalled;
        protected static bool IsMechJebInstalled
        {
            get
            {
                if (!_isMechJebInstalled.HasValue)
                {
                    _isMechJebInstalled = AssemblyLoader.loadedAssemblies.Any(a => a.assembly.GetName().Name == "MechJeb2");
                }
                return _isMechJebInstalled.Value;
            }
        }

        internal void Awake()
        {
            // Except that KSP Startup enum is stupid and we don't actually want Flight scene
            if (HighLogic.LoadedSceneIsFlight)
                Destroy(this);
        }

        internal void Start()
        {
            _uiScale = GameSettings.UI_SCALE;

            if (HighLogic.LoadedSceneIsEditor)
            {
                int _offset = 260;
                if (IsMechJebInstalled)
                    _offset += 140;
                else if (SteamManager.Initialized)
                    _offset += 46;
                var btn = new GUI_DevPartsButton(_offset)
                {
                    DefaultTexturePath = "GameData/RP-1/PluginData/Icons/KCT_dev_parts_off",
                    DefaultHovTexturePath = "GameData/RP-1/PluginData/Icons/KCT_dev_parts_off",
                    OnTexturePath = "GameData/RP-1/PluginData/Icons/KCT_dev_parts_on",
                    OnHovTexturePath = "GameData/RP-1/PluginData/Icons/KCT_dev_parts_on",
                    TooltipDefaultText = "Show Experimental parts",
                    TooltipOnText = "Hide Experimental parts",
                    IsOn = SpaceCenterManagement.Instance.ExperimentalPartsEnabled
                };
                btn.Init();
                _buttons.Add(btn);
            }
            else if (HighLogic.LoadedScene == GameScenes.SPACECENTER)
            {
                var btn = new GUI_AbMcButton(83)
                {
                    TooltipDefaultText = "Switch to Administration Building",
                    TooltipOnText = "Switch to Mission Control",
                    DefaultTexturePath = "GameData/RP-1/PluginData/Icons/adm",
                    DefaultHovTexturePath = "GameData/RP-1/PluginData/Icons/adm_hov",
                    OnTexturePath = "GameData/RP-1/PluginData/Icons/mc",
                    OnHovTexturePath = "GameData/RP-1/PluginData/Icons/mc_hov"
                };
                btn.Init();
                _buttons.Add(btn);
            }
        }

        internal void OnGUI()
        {
            if (_uiScale != GameSettings.UI_SCALE)
            {
                foreach (var b in _buttons)
                {
                    b.RescaleAndSetTextures();
                }
                _uiScale = GameSettings.UI_SCALE;
            }

            foreach (var b in _buttons)
            {
                b.OnGUI();
            }
        }
    }
}
