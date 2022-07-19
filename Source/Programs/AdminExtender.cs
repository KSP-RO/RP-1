// Ported from Strategia by nightingale/jrossignol.

using System;
using System.Collections.Generic;
using System.Linq;
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
        public AdministrationActiveTabView ActiveTabView => _tabController == null ? AdministrationActiveTabView.Active : (AdministrationActiveTabView)CurrentListField.GetValue(_tabController);

        protected void ToggleProgramTab(bool active)
        {
            if (active)
                Administration.Instance.RedrawPanels();
        }

        public void BindAndFixUI()
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

            if (transform.FindDeepChild("speedButtonSlow") != null)
                return; // done.

            // Add new UI bits
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

            for(int i = 0; i < newTabSetObj.transform.childCount; ++i)
            {
                Transform child = newTabSetObj.transform.GetChild(i);
                GameObject.Destroy(child.gameObject.GetComponent<UIList>());
                child.gameObject.GetComponent<Toggle>().onValueChanged.AddListener(ToggleProgramTab);
                child.gameObject.GetComponent<LayoutElement>().preferredWidth = 180;
                foreach (var text in child.gameObject.GetComponentsInChildren<TMPro.TextMeshProUGUI>())
                {
                    if (i == 0)
                        text.text = KSP.Localization.Localizer.GetStringByTag("#rp0ActivePrograms");
                    else if (i == 1)
                        text.text = KSP.Localization.Localizer.GetStringByTag("#rp0CompletedPrograms");
                    else
                        text.text = KSP.Localization.Localizer.GetStringByTag("#rp0Leaders");
                }
            }

            // Finally, parent the Strat Count text to the new tab object *after* we fixed the tabs.
            var lE = oldText.gameObject.AddComponent<LayoutElement>();
            oldText.SetParent(newTabSetObj.transform, false);
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