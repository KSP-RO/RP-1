// Ported from Strategia by nightingale/jrossignol.

using System;
using System.Collections.Generic;
using UniLinq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KSP.UI;
using KSP.UI.Screens;
using Strategies;
using ContractConfigurator;
using ContractConfigurator.Util;
using KSP.Localization;

namespace RP0.Programs
{
    public enum AdministrationActiveTabView
    {
        Active = 0,
        Completed,
        Leaders,

        MAX
    }

    public class AdminExtender : MonoBehaviour
    {
        public static AdminExtender Instance;

        private static FieldInfo CurrentListField = typeof(UIListToggleController).GetField("currentList", BindingFlags.NonPublic | BindingFlags.Instance);
        private UIListToggleController _tabController;
        private readonly Dictionary<AdministrationActiveTabView, Toggle> _adminTabToggles = new Dictionary<AdministrationActiveTabView, Toggle>();
        public AdministrationActiveTabView ActiveTabView => _tabController == null ? AdministrationActiveTabView.Active : (AdministrationActiveTabView)CurrentListField.GetValue(_tabController);

        private void OnClickTab(bool active)
        {
            if (active)
                Administration.Instance.RedrawPanels();
        }

        public void SetTabView(AdministrationActiveTabView view)
        {
            if (ActiveTabView == view)
                return;

            _adminTabToggles[view].isOn = true;
        }

        private LayoutElement _btnSpacer = null;
        public LayoutElement BtnSpacer => _btnSpacer;

        private readonly Dictionary<Program.Speed, ProgramSpeedListItem> _speedButtons = new Dictionary<Program.Speed, ProgramSpeedListItem>();
        private KSP.UI.TooltipTypes.UIStateButtonTooltip _buttonTooltip;

        public bool PressedSpeedButton = false;

        private TMPro.TextMeshProUGUI _speedOptionsNames;
        private TMPro.TextMeshProUGUI _speedOptionsCosts;

        public void SetSpeedButtonsActive(Program program)
        {
            bool active = program != null;

            string costStr = string.Empty;
            foreach (var item in _speedButtons.Values)
            {
                item.gameObject.SetActive(active);
                if (active)
                {
                    double cost = program.GetDisplayedConfidenceCostForSpeed(item.Speed);
                    bool allowable = program.IsSpeedAllowed(item.Speed);
                    var sph = new SpeedButtonHolder(program.GetStrategy(), item.Speed);
                    item.SetupButton(allowable, Localizer.Format("#rp0ProgramSpeedConfidenceRequired", cost.ToString("N0")), program, sph.OnSpeedSet, sph.OnSpeedUnset);
                    costStr += "\n" + cost.ToString("N0");
                }
            }

            _speedOptionsNames.gameObject.SetActive(active);
            _speedOptionsCosts.gameObject.SetActive(active);

            if (active)
            {
                _speedButtons[program.speed].SetState(UIRadioButton.State.True);
                _speedOptionsCosts.text = Localizer.GetStringByTag("#rp0ProgramConfidenceCost") + costStr;
            }

            PressedSpeedButton = false; // clear state
        }

        public class SpeedButtonHolder
        {
            private static MethodInfo GetStrategyDescriptionMethod = typeof(Administration).GetMethod("GetStrategyDescription", BindingFlags.NonPublic | BindingFlags.Instance);

            private ProgramStrategy programStrategy;
            private Program.Speed speed;

            public SpeedButtonHolder(ProgramStrategy ps, Program.Speed s)
            {
                programStrategy = ps;
                speed = s;
            }

            public void OnSpeedSet(UnityEngine.EventSystems.PointerEventData data, UIRadioButton.CallType callType)
            {
                var item = AdminExtender.Instance._speedButtons[speed];
                item.ResetInteractivity(true);
                var oldSpeed = programStrategy.Program.speed;
                programStrategy.Program.speed = speed;

                // Update the description and the button on change
                if (oldSpeed != speed)
                {
                    AdminExtender.Instance.PressedSpeedButton = true; // so we don't reset program speed
                    Administration.Instance.SetSelectedStrategy(Administration.Instance.SelectedWrapper);
                }
            }

            public void OnSpeedUnset(UnityEngine.EventSystems.PointerEventData data, UIRadioButton.CallType callType)
            {
                AdminExtender.Instance._speedButtons[speed].ResetInteractivity(false);
            }
        }

        private void RescaleMainUI()
        {
            // Resize the root element that handles the width
            Transform aspectFitter = transform.FindDeepChild("bg and aspectFitter");
            if (aspectFitter != null)
            {
                RectTransform rect = aspectFitter.GetComponent<RectTransform>();

                // Determine the ideal size
                // was -4, using -3 since programs is double-wide
                int count = Math.Max(StrategySystem.Instance.SystemConfig.Departments.Count - 3, 0);
                float size = Math.Min(944f + (count * 232.0f), Screen.width);

                rect.sizeDelta = new Vector2(size, rect.sizeDelta.y);
            }
        }

        private void AddTabs()
        {
            Transform oldTabTransform = transform.FindDeepChild("ActiveStrategiesTab");
            Transform oldText = oldTabTransform.parent.transform.Find("ActiveStratCount");
            // Grab tabs from AC
            var ACSpawner = TimeWarp.fetch.gameObject.GetComponent<ACSceneSpawner>(); // Timewarp lives on the SpaceCenter object
            // Have to do it this way because it's inactive
            Transform[] ACtransforms = ACSpawner.ACScreenPrefab.transform.GetComponentsInChildren<Transform>(true);
            var tabAvailable = ACtransforms.First(t => t.name == "Tab Available");

            // Now we can spawn the parent (the Tabs transform)
            GameObject newTabSetObj = GameObject.Instantiate(tabAvailable.parent.gameObject);

            // Position it correctly and kill the old tab.
            newTabSetObj.transform.SetParent(oldTabTransform.parent, false);
            newTabSetObj.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            GameObject.Destroy(oldTabTransform.gameObject);

            // Fix the Toggle Controller to always switch to the same list (we *could* try to manage three lists
            // but instead we'll just fix it in Redraw)
            _tabController = newTabSetObj.GetComponent<UIListToggleController>();
            _tabController.lists = new UIList[] { Administration.Instance.scrollListActive, Administration.Instance.scrollListActive, Administration.Instance.scrollListActive };

            // Ok it's safe to set active and fix the tab text
            newTabSetObj.SetActive(true);

            for (int i = 0; i < newTabSetObj.transform.childCount; ++i)
            {
                Transform child = newTabSetObj.transform.GetChild(i);
                GameObject.Destroy(child.gameObject.GetComponent<UIList>());
                child.gameObject.GetComponent<Toggle>().onValueChanged.AddListener(OnClickTab);
                child.gameObject.GetComponent<LayoutElement>().preferredWidth = 180;
                foreach (var text in child.gameObject.GetComponentsInChildren<TMPro.TextMeshProUGUI>())
                {
                    if (i == 0)
                        text.text = Localizer.GetStringByTag("#rp0ActivePrograms");
                    else if (i == 1)
                        text.text = Localizer.GetStringByTag("#rp0CompletedPrograms");
                    else
                        text.text = Localizer.GetStringByTag("#rp0Leaders");
                }
                _adminTabToggles[(AdministrationActiveTabView)i] = child.GetComponentInChildren<Toggle>();
            }

            // Finally, parent the Strat Count text to the new tab object *after* we fixed the tabs.
            var lE = oldText.gameObject.AddComponent<LayoutElement>();
            oldText.SetParent(newTabSetObj.transform, false);
        }

        private void AddSpeedButtons()
        {
            var buttonLE = Administration.Instance.btnAcceptCancel.gameObject.AddComponent<LayoutElement>();
            buttonLE.preferredWidth = 48;
            buttonLE.preferredHeight = 50;
            var buttonArea = buttonLE.transform.parent.gameObject;
            // add layout group
            var hLG = buttonArea.AddComponent<HorizontalLayoutGroup>();
            hLG.childForceExpandHeight = false;
            hLG.childForceExpandWidth = false;
            hLG.childAlignment = TextAnchor.MiddleRight;
            hLG.spacing = 10;

            // Create speed buttons
            var tooltipPrefab = AssetBase.GetPrefab<KSP.UI.TooltipTypes.Tooltip_Text>("Tooltip_Text");
            int max = (int)Program.Speed.MAX;
            int width = (int)(285.0f / max);
            TextMeshProUGUI srcText = null;
            for (int i = 0; i < max; ++i)
            {
                Program.Speed spd = (Program.Speed)i;

                var newButton = GameObject.Instantiate(Administration.Instance.prefabStratListItem, buttonArea.transform, worldPositionStays: false);
                newButton.gameObject.name = "speedButton" + i;
                GameObject.Destroy(newButton.GetComponent<StrategyListItem>());

                var textRect = newButton.transform.Find("btn").transform.Find("Text").GetComponent<RectTransform>();
                textRect.offsetMin = new Vector2(2, 2);
                textRect.offsetMax = new Vector2(-2, -2);
                var textComp = textRect.GetComponent<TextMeshProUGUI>();
                srcText = textComp;
                textComp.alignment = TextAlignmentOptions.Midline;
                textComp.fontSizeMax = 20;

                var tooltip = newButton.gameObject.AddComponent<KSP.UI.TooltipTypes.TooltipController_Text>();
                tooltip.prefab = tooltipPrefab;
                tooltip.RequireInteractable = false; // some of these buttons may not be interactable
                tooltip.textString = "";

                var speedItem = newButton.gameObject.AddComponent<ProgramSpeedListItem>();
                newButton.gameObject.SetActive(true); // so Awake runs, if it hasn't
                speedItem.Initialize(Localizer.GetStringByTag("#rp0ProgramSpeed" + i), spd);
                _speedButtons[spd] = speedItem;

                // Set layout
                var lE = newButton.gameObject.AddComponent<LayoutElement>();
                lE.preferredHeight = 54;
                lE.preferredWidth = width;
            }
            // Add a spacer between the speed buttons and the accept button
            var newSpacerLE = GameObject.Instantiate(ApplicationLauncher.Instance.CurrentLayout.GetTopRightSpacer(), buttonArea.transform, worldPositionStays: false);
            newSpacerLE.preferredWidth = 100;
            newSpacerLE.gameObject.SetActive(true);

            // Add a spacer to replace the button
            _btnSpacer = GameObject.Instantiate(ApplicationLauncher.Instance.CurrentLayout.GetTopRightSpacer(), buttonArea.transform, worldPositionStays: false);
            _btnSpacer.preferredWidth = 48;
            _btnSpacer.gameObject.SetActive(false);

            // Add text for costs
            _speedOptionsNames = Instantiate(srcText, buttonArea.transform, worldPositionStays: false);
            _speedOptionsNames.fontSizeMax = 15;
            var namesLE = _speedOptionsNames.gameObject.AddComponent<LayoutElement>();
            namesLE.preferredWidth = 120;
            _speedOptionsNames.alignment = TextAlignmentOptions.MidlineLeft;
            string namesStr = Localizer.GetStringByTag("#rp0ProgramSpeed");
            for (int i = 0; i < max; ++i)
            {
                namesStr += "\n" + Localizer.GetStringByTag("#rp0ProgramSpeed" + i);
            }
            _speedOptionsNames.text = namesStr;

            _speedOptionsCosts = Instantiate(_speedOptionsNames, buttonArea.transform, worldPositionStays: false);
            _speedOptionsCosts.alignment = TextAlignmentOptions.MidlineRight;

            _speedOptionsCosts.transform.SetAsFirstSibling();
            _speedOptionsNames.transform.SetAsFirstSibling();

            _speedOptionsNames.gameObject.SetActive(false);
            _speedOptionsCosts.gameObject.SetActive(false);

            // make sure the button (and the spacer replacement) is at the far right
            buttonLE.transform.SetAsLastSibling();
            _btnSpacer.transform.SetAsLastSibling();

            foreach (var button in _speedButtons.Values)
                button.gameObject.SetActive(false);
        }

        public void BindAndFixUI()
        {
            _buttonTooltip = Administration.Instance.btnAcceptCancel.GetComponent<KSP.UI.TooltipTypes.UIStateButtonTooltip>();

            RescaleMainUI();

            // If we've already created the new stuff, find it and bail
            if (transform.FindDeepChild("speedButton0") != null)
            {
                var buttonArea = Administration.Instance.btnAcceptCancel.transform.parent;
                ProgramSpeedListItem[] items = buttonArea.GetComponentsInChildren<ProgramSpeedListItem>(true);
                foreach (var item in items)
                {
                    _speedButtons[item.Speed] = item;
                }

                return;
            }

            AddTabs();
            AddSpeedButtons();
        }
        
        public void Awake()
        {
            if (Instance != null)
            {
                GameObject.Destroy(Instance);
            }
            Instance = this;
        }

        public void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}