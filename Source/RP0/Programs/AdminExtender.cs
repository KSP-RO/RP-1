// Ported from Strategia by nightingale/jrossignol.

using System;
using System.Collections.Generic;
using UniLinq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KSP.UI;
using KSP.UI.Screens;
using Strategies;
using KSP.Localization;
using ROUtils;

namespace RP0.Programs
{
    public enum AdministrationActiveTabView
    {
        Active = 0,
        Completed,
        Leaders,

        MAX
    }

    /// <summary>
    /// Helper class for the Administration UI since we can't add fields to stock classes.
    /// This stores state (and gameobject links) for Administration
    /// </summary>
    public class AdminExtender : MonoBehaviour
    {
        public static AdminExtender Instance;

        private float _uiWidth;

        /// <summary>
        /// This controls the tabs at the bottom of the screen (swapping between active and complete programs, and active leaders)
        /// </summary>
        private UIListToggleController _tabController;
        /// <summary>
        /// These are the tab toggle buttons
        /// </summary>
        private readonly Dictionary<AdministrationActiveTabView, Toggle> _adminTabToggles = new Dictionary<AdministrationActiveTabView, Toggle>();
        /// <summary>
        /// Getter for the active tab view.
        /// </summary>
        public AdministrationActiveTabView ActiveTabView => _tabController == null ? AdministrationActiveTabView.Active : (AdministrationActiveTabView)_tabController.currentList;

        private void OnClickTab(bool active)
        {
            // Force a whole redraw. Bleh, but it's fine.
            if (active)
                Administration.Instance.RedrawPanels();
        }

        public void SetTabView(AdministrationActiveTabView view)
        {
            if (ActiveTabView == view)
                return;

            _adminTabToggles[view].isOn = true; // this will trip OnClickTab.
        }

        private LayoutElement _btnSpacer = null;
        public LayoutElement BtnSpacer => _btnSpacer;

        /// <summary>
        /// We need a dictionary of the speeds and their buttons
        /// </summary>
        private readonly Dictionary<Program.Speed, ProgramSpeedListItem> _speedButtons = new Dictionary<Program.Speed, ProgramSpeedListItem>();
        private KSP.UI.TooltipTypes.UIStateButtonTooltip _buttonTooltip;

        /// <summary>
        /// Gross global to pass data between Administration and the speed buttons
        /// </summary>
        public bool PressedSpeedButton = false;

        private TextMeshProUGUI _speedOptionsNames;
        private TextMeshProUGUI _speedOptionsCosts;

        private ProgramFundingOverview _fundingOverview;

        public void SetSpeedButtonsActive(Program program)
        {
            // If no program is set, then no buttons can be active.
            bool active = program != null;

            // Otherwise, setup the buttons with their costs
            string costStr = string.Empty;
            foreach (var item in _speedButtons.Values)
            {
                item.gameObject.SetActive(active);
                if (active)
                {
                    double cost = program.GetDisplayedConfidenceCostForSpeed(item.Speed);
                    bool allowable = program.IsSpeedAllowed(item.Speed);
                    var sph = new SpeedButtonHolder(program.GetStrategy(), item.Speed);
                    item.SetupButton(allowable, Localizer.Format("#rp0_Admin_Program_ConfidenceRequired", cost.ToString("N0")), program, sph.OnSpeedSet, sph.OnSpeedUnset);
                    costStr += "\n" + cost.ToString("N0");
                }
            }

            _speedOptionsNames.gameObject.SetActive(active);
            _speedOptionsCosts.gameObject.SetActive(active);

            if (active)
            {
                _speedButtons[program.ProgramSpeed].SetState(UIRadioButton.State.True);
                _speedOptionsCosts.text = Localizer.Format("#rp0_Admin_Program_ConfidenceCost") + costStr;
            }

            PressedSpeedButton = false; // clear state
        }

        public void SetFundingGraphActive(Program program)
        {
            _fundingOverview.SetupProgram(program);
        }

        /// <summary>
        /// We need this gross class (like StrategyWrapper) to handle the linkage between the button and the current active program
        /// So when you click the button, the program gets the input
        /// </summary>
        public class SpeedButtonHolder
        {
            private ProgramStrategy programStrategy;
            private Program.Speed speed;

            public SpeedButtonHolder(ProgramStrategy ps, Program.Speed s)
            {
                programStrategy = ps;
                speed = s;
            }

            public void OnSpeedSet(UnityEngine.EventSystems.PointerEventData data, UIRadioButton.CallType callType)
            {
                // Find the speedbutton of this speed
                var item = Instance._speedButtons[speed];
                item.ResetInteractivity(true);
                var oldSpeed = programStrategy.Program.ProgramSpeed;
                programStrategy.Program.SetSpeed(speed); // no-op if same speed

                // Update the description and the button on change
                if (oldSpeed != speed)
                {
                    Instance.PressedSpeedButton = true; // so we don't reset program speed when we rebuild UI
                    // Rebuild the UI
                    Administration.Instance.SetSelectedStrategy(Administration.Instance.SelectedWrapper);
                }
            }

            public void OnSpeedUnset(UnityEngine.EventSystems.PointerEventData data, UIRadioButton.CallType callType)
            {
                // These are radio buttons, so this runs on the buttons that just got inactivated
                Instance._speedButtons[speed].ResetInteractivity(false);
            }
        }

        /// <summary>
        /// This code just makes the main UI bigger to better take advantage of wide screens.
        /// </summary>
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
                _uiWidth = Math.Min(944f + (count * 232.0f), Screen.width);

                rect.sizeDelta = new Vector2(_uiWidth, rect.sizeDelta.y);
            }
        }

        /// <summary>
        /// This adds the tab buttons to the active strategy tab
        /// (We steal them from the AC)
        /// </summary>
        private void AddTabs()
        {
            Transform oldTabTransform = transform.FindDeepChild("ActiveStrategiesTab");
            Transform oldText = oldTabTransform.parent.transform.Find("ActiveStratCount");
            // Grab tabs from Astronaut Complex UI
            var ACSpawner = TimeWarp.fetch.gameObject.GetComponent<ACSceneSpawner>(); // Timewarp lives on the SpaceCenter object
            // Have to do it this way because it's inactive
            Transform[] ACtransforms = ACSpawner.ACScreenPrefab.transform.GetComponentsInChildren<Transform>(true);
            var tabAvailable = ACtransforms.First(t => t.name == "Tab Available");

            // Now we can spawn the parent (the Tabs transform)
            GameObject newTabSetObj = Instantiate(tabAvailable.parent.gameObject);

            // Position it correctly and kill the old tab.
            newTabSetObj.transform.SetParent(oldTabTransform.parent, false);
            newTabSetObj.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            Destroy(oldTabTransform.gameObject);

            // Fix the Toggle Controller to always switch to the same list (we *could* try to manage three lists
            // but instead we'll just fix it in Redraw)
            _tabController = newTabSetObj.GetComponent<UIListToggleController>();
            _tabController.lists = new UIList[] { Administration.Instance.scrollListActive, Administration.Instance.scrollListActive, Administration.Instance.scrollListActive };

            // Ok it's safe to set active and fix the tab text
            newTabSetObj.SetActive(true);

            // Setup the actual tabs we just grabbed
            for (int i = 0; i < newTabSetObj.transform.childCount; ++i)
            {
                Transform child = newTabSetObj.transform.GetChild(i);
                Destroy(child.gameObject.GetComponent<UIList>());
                child.gameObject.GetComponent<Toggle>().onValueChanged.AddListener(OnClickTab);
                child.gameObject.GetComponent<LayoutElement>().preferredWidth = 180;
                foreach (var text in child.gameObject.GetComponentsInChildren<TextMeshProUGUI>())
                {
                    if (i == 0)
                        text.text = Localizer.Format("#rp0_Admin_ActivePrograms");
                    else if (i == 1)
                        text.text = Localizer.Format("#rp0_Admin_CompletedPrograms");
                    else
                        text.text = Localizer.Format("#rp0_Leaders_Title");
                }
                _adminTabToggles[(AdministrationActiveTabView)i] = child.GetComponentInChildren<Toggle>();
            }

            // Finally, parent the Strat Count text to the new tab object *after* we fixed the tabs.
            var lE = oldText.gameObject.AddComponent<LayoutElement>();
            oldText.SetParent(newTabSetObj.transform, false);
        }

        /// <summary>
        /// Here we create the speed buttons. They're stolen from existing buttons
        /// and repurposed. To do this we need to add a layout group.
        /// </summary>
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

                var newButton = Instantiate(Administration.Instance.prefabStratListItem, buttonArea.transform, worldPositionStays: false);
                newButton.gameObject.name = "speedButton" + i;
                Destroy(newButton.GetComponent<StrategyListItem>());

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
                speedItem.Initialize(Localizer.GetStringByTag("#rp0_Admin_Program_Speed" + i), spd);
                _speedButtons[spd] = speedItem;

                // Set layout
                var lE = newButton.gameObject.AddComponent<LayoutElement>();
                lE.preferredHeight = 54;
                lE.preferredWidth = width;
            }
            // Add a spacer between the speed buttons and the accept button
            var newSpacerLE = Instantiate(ApplicationLauncher.Instance.CurrentLayout.GetTopRightSpacer(), buttonArea.transform, worldPositionStays: false);
            newSpacerLE.preferredWidth = 100;
            newSpacerLE.gameObject.SetActive(true);

            // Add a spacer to replace the button (sometimes the accept button isn't shown at all)
            _btnSpacer = Instantiate(ApplicationLauncher.Instance.CurrentLayout.GetTopRightSpacer(), buttonArea.transform, worldPositionStays: false);
            _btnSpacer.preferredWidth = 48;
            _btnSpacer.gameObject.SetActive(false);

            // Add text for costs
            _speedOptionsNames = Instantiate(srcText, buttonArea.transform, worldPositionStays: false);
            _speedOptionsNames.fontSizeMax = 15;
            var namesLE = _speedOptionsNames.gameObject.AddComponent<LayoutElement>();
            namesLE.preferredWidth = 120;
            _speedOptionsNames.alignment = TextAlignmentOptions.MidlineLeft;
            string namesStr = Localizer.GetStringByTag("#rp0_Admin_Program_Speed");
            for (int i = 0; i < max; ++i)
            {
                namesStr += "\n" + Localizer.GetStringByTag("#rp0_Admin_Program_Speed" + i);
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

        private void AddProgramFundingOverview()
        {
            var _fundingGraphPanel = new GameObject("ProgramFundingGraphArea", typeof(RectTransform));
            _fundingGraphPanel.transform.SetParent(Administration.Instance.textPanel.transform);
            _fundingOverview = _fundingGraphPanel.gameObject.AddComponent<ProgramFundingOverview>();

            _fundingGraphPanel.gameObject.SetActive(true);
        }

        private void AddBottomScrollbar()
        {
            Transform scrollbarOld = transform.FindDeepChild("Scrollbar H");
            Transform bottomArea = transform.FindDeepChild("Panel_bottom").FindDeepChild("ListAndScrollbar");
            Destroy(bottomArea.gameObject.GetComponent<HorizontalLayoutGroup>());
            RectTransform newScrollbar = Instantiate(scrollbarOld.gameObject, bottomArea).GetComponent<RectTransform>();
            ScrollRect scroll = bottomArea.FindDeepChild("ScrollRect").GetComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.horizontalScrollbar = newScrollbar.GetComponent<Scrollbar>();
            scroll.viewport = scroll.GetComponent<RectTransform>();
            var panelRT = scroll.transform.parent.GetComponent<RectTransform>();
            panelRT.sizeDelta = new Vector2(_uiWidth - 12f, panelRT.sizeDelta.y - 16f);
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
            AddProgramFundingOverview();
            AddBottomScrollbar();
        }

        public void Awake()
        {
            if (Instance != null)
            {
                Destroy(Instance);
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