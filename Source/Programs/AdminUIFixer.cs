// Ported from Strategia by nightingale/jrossignol.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KSP;
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
    /// <summary>
    /// Special MonoBehaviour to fix admin building UI.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public class AdminUIFixer : MonoBehaviour
    {
        public static AdminUIFixer Instance;
        protected int _ticks = 0;

        protected AdministrationActiveTabView _activeTabView = AdministrationActiveTabView.Active;
        public AdministrationActiveTabView ActiveTabView => _activeTabView;
        protected Transform _tabTransform;
        protected TextMeshProUGUI _tabText;

        protected void ToggleProgramTab()
        {
            _activeTabView = (AdministrationActiveTabView)(_activeTabView + 1);
            if (_activeTabView == AdministrationActiveTabView.MAX)
                _activeTabView = AdministrationActiveTabView.Active;

            SetTabState();
            Administration.Instance.RedrawPanels();
        }

        protected void SetTabState()
        {
            string s;
            switch (_activeTabView)
            {
                default:
                case AdministrationActiveTabView.Active: s = "[Active] Completed Staff"; break;
                case AdministrationActiveTabView.Completed: s = "Active [Completed] Staff"; break;
                case AdministrationActiveTabView.Leaders: s = "Active Completed [Staff]"; break;
            }
            _tabText.text = s;
        }
        
        public void Awake()
        {
            if (Instance != null)
            {
                GameObject.Destroy(Instance);
            }
            Instance = this;
        }

        public void Update()
        {
            // Wait for the admin UI to get loaded
            if (KSP.UI.Screens.Administration.Instance == null)
            {
                _ticks = 0;
                _activeTabView = AdministrationActiveTabView.Active;
                return;
            }

            if (_ticks++ == 0)
            {
                // Resize the root element that handles the width
                Transform aspectFitter = KSP.UI.Screens.Administration.Instance.transform.FindDeepChild("bg and aspectFitter");
                if (aspectFitter != null)
                {
                    RectTransform rect = aspectFitter.GetComponent<RectTransform>();

                    // Determine the ideal size
                    int count = Math.Max(StrategySystem.Instance.SystemConfig.Departments.Count - 4, 0);
                    float size = Math.Min(944f + (count * 232.0f), Screen.width);

                    rect.sizeDelta = new Vector2(size, rect.sizeDelta.y);
                }

                // Fix text
                _tabTransform = Administration.Instance.transform.FindDeepChild("ActiveStrategiesTab");
                _tabText = _tabTransform.Find("Text").GetComponent<TextMeshProUGUI>();
                SetTabState();
                Button b = _tabTransform.gameObject.GetComponent<Button>() ?? _tabTransform.gameObject.AddComponent<Button>();
                b.onClick.AddListener(ToggleProgramTab);
                
                // Replace department avatars with images when necessary
                Transform scrollListKerbals = KSP.UI.Screens.Administration.Instance.transform.FindDeepChild("scroll list kerbals");
                foreach (DepartmentConfig department in StrategySystem.Instance.SystemConfig.Departments)
                {
                    // If there is no avatar prefab but there is a head image, use that in place
                    if (department.AvatarPrefab == null)
                    {
                        // Get the head image
                        Texture2D tex = department.HeadImage;
                        if (tex == null)
                        {
                            // Pull from texture DB if possible
                            if (GameDatabase.Instance.ExistsTexture(department.HeadImageString))
                            {
                                tex = GameDatabase.Instance.GetTexture(department.HeadImageString, false);
                            }
                            // Otherwise just load it
                            else
                            {
                                tex = TextureUtil.LoadTexture(department.HeadImageString);
                            }
                        }

                        for (int i = 0; i < scrollListKerbals.childCount; i++)
                        {
                            Transform t = scrollListKerbals.GetChild(i);
                            KerbalListItem kerbalListItem = t.GetComponent<KerbalListItem>();
                            if (kerbalListItem.title.text.Contains(department.HeadName))
                            {
                                kerbalListItem.kerbalImage.texture = tex;
                                kerbalListItem.kerbalImage.material = kerbalListItem.kerbalImage.defaultMaterial;

                                // Remove extra braces
                                if (kerbalListItem.title.text.Contains("()"))
                                {
                                    kerbalListItem.title.text.Replace("()", "");
                                }

                                break;
                            }
                        }
                    }
                }

            }
        }
    }

    public static class TransformExtns
    {
        public static Transform FindDeepChild(this Transform parent, string name)
        {
            var result = parent.Find(name);
            if (result != null)
                return result;
            foreach (Transform child in parent)
            {
                result = child.FindDeepChild(name);
                if (result != null)
                    return result;
            }
            return null;
        }

        public static void Dump(this GameObject go, string indent = "")
        {
            Debug.Log(indent + "Object: " + go.name);
            foreach (Component c in go.GetComponents<Component>())
            {
                Debug.Log(indent + c);
                if (c is TextMeshProUGUI t)
                    Debug.Log(indent + "   text=" + t.text);
            }

            foreach (Transform c in go.transform)
            {
                c.gameObject.Dump(indent + "    ");
            }
        }
    }
}