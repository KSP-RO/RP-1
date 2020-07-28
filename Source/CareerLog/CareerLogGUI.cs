using System;
using System.IO;
using UnityEngine;

namespace RP0
{
    public class CareerLogGUI : UIBase
    {
        private string _exportStatus;
        private string _exportStatusWeb;

        public void RenderTab()
        {
            GUILayout.BeginVertical();
            if (GUILayout.Button("Export to file", HighLogic.Skin.button, GUILayout.ExpandWidth(false), GUILayout.Height(30), GUILayout.Width(125)))
            {
                try
                {
                    string path = KSPUtil.ApplicationRootPath + "/RP-1_Career.csv";
                    CareerLog.Instance.ExportToFile(path);
                    _exportStatus = $"Career progress exported to {Path.GetFullPath(path)}";
                }
                catch (Exception ex)
                {
                    _exportStatus = $"Export failed: {ex.Message}";
                }
            }
            GUILayout.Label(_exportStatus);

            if (GUILayout.Button("Export to web", GUILayout.ExpandWidth(false), GUILayout.Height(30),
                GUILayout.Width(125)))
            {
                try
                {
                    CareerLog.Instance.ExportToWeb();
                    _exportStatusWeb = "Career progress exported to web.";
                }
                catch (Exception ex)
                {
                    _exportStatusWeb = $"Export failed: {ex.Message}";
                }
            }
            GUILayout.Label(_exportStatusWeb);
            GUILayout.Label("CareerLog id:");
            GUILayout.TextField($"{SystemInfo.deviceUniqueIdentifier}");

            GUILayout.EndVertical();
        }
    }
}
