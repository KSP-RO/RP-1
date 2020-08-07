using System;
using System.IO;
using UnityEngine;

namespace RP0
{
    public class CareerLogGUI : UIBase
    {
        private string _exportStatus;
        private string _exportStatusWeb;
        private string _serverUrl;

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

            GUILayout.Label("Server URL:");
            _serverUrl = GUILayout.TextField(_serverUrl);

            if (GUILayout.Button("Export to web", GUILayout.ExpandWidth(false), GUILayout.Height(30),
                GUILayout.Width(125)))
            {
                try
                {
                    CareerLog.Instance.ExportToWeb(_serverUrl);
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
