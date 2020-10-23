using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace RP0
{
    public class CareerLogGUI : UIBase
    {
        private string _exportStatus;
        private string _exportStatusWeb;
        private string _serverUrl;
        private string _token;

        protected override void OnStart()
        {
            base.OnStart();

            var settings = HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>();
            if (!string.IsNullOrWhiteSpace(settings?.CareerLog_URL))
            {
                byte[] bytes = Convert.FromBase64String(settings.CareerLog_URL);
                _serverUrl = Encoding.UTF8.GetString(bytes);
            }
            _token = settings?.CareerLog_Token;
        }

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
            _serverUrl = GUILayout.TextField(_serverUrl, HighLogic.Skin.textField);

            GUILayout.Label("Token:");
            _token = GUILayout.TextField(_token, HighLogic.Skin.textField);

            if (GUILayout.Button("Export to web", HighLogic.Skin.button, GUILayout.ExpandWidth(false), GUILayout.Height(30),
                GUILayout.Width(125)))
            {
                try
                {
                    var settings = HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>();
                    settings.CareerLog_URL = Convert.ToBase64String(Encoding.UTF8.GetBytes(_serverUrl));    // KSP really doesn't like the symbols that a typical URL contains
                    settings.CareerLog_Token = _token;

                    CareerLog.Instance.ExportToWeb(_serverUrl);
                    _exportStatusWeb = "Career progress exported to web.";
                }
                catch (Exception ex)
                {
                    _exportStatusWeb = $"Export failed: {ex.Message}";
                }
            }
            GUILayout.Label(_exportStatusWeb);

            GUILayout.EndVertical();
        }
    }
}
