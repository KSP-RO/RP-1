using System;
using System.IO;
using UnityEngine;

namespace RP0
{
    public class CareerLogGUI : UIBase
    {
        private string _exportStatus;

        public void RenderTab()
        {
            GUILayout.BeginVertical();
            if (GUILayout.Button("Export to file", GUILayout.ExpandWidth(false), GUILayout.Height(30), GUILayout.Width(125)))
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
            GUILayout.EndVertical();
        }
    }
}
