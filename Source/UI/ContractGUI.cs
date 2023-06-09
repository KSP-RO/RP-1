using System;
using UnityEngine;

namespace RP0
{
    public class ContractGUI : UIBase
    {
        public const int MinPayload = 400;
        public const int MaxPayload = 10000;

        public static int CommsPayload = MinPayload;
        public static int WeatherPayload = MinPayload;

        public static Func<string, bool> WithdrawContractAction;

        private static string[] _comSatContracts = new[] { "GEORepeatComSats", "TundraRepeatComSats", "MolniyaRepeatComSats" };
        private static string[] _weatherSatContracts = new[] { "GEOWeather" };

        private static string NewspaperTitle = "Space Gazette";

        private RP0Settings _settings;

        protected override void OnStart()
        {
            _settings = HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>();
            CommsPayload = _settings.CommsPayload;
            WeatherPayload = _settings.WeatherPayload;
            NewspaperTitle = _settings.NewspaperTitle;
        }

        public void RenderContractsTab()
        {
            GUILayout.BeginVertical();
            try
            {
                GUILayout.Label($"Use this tab to set the required payload amount for contracts.", BoldLabel);
                GUILayout.Space(10f);

                GUILayout.Label($"CommSat Payload range: {Math.Max(CommsPayload / 2, 300)} - {CommsPayload}", HighLogic.Skin.label, GUILayout.Width(250));
                float commsAmnt = GUILayout.HorizontalSlider(CommsPayload, MinPayload, MaxPayload, HighLogic.Skin.horizontalSlider, HighLogic.Skin.horizontalSliderThumb);
                CommsPayload = Mathf.RoundToInt(commsAmnt / 100) * 100;    // slider works in increments of 100
                GUILayout.Space(5f);
                GUILayout.Label($"WeatherSat Payload range: {Math.Max(WeatherPayload / 2, 300)} - {WeatherPayload}", HighLogic.Skin.label, GUILayout.Width(250));
                float weatherAmnt = GUILayout.HorizontalSlider(WeatherPayload, MinPayload, MaxPayload, HighLogic.Skin.horizontalSlider, HighLogic.Skin.horizontalSliderThumb);
                WeatherPayload = Mathf.RoundToInt(weatherAmnt / 100) * 100;

                if (_settings.CommsPayload != CommsPayload)
                {
                    foreach (string contractName in _comSatContracts)
                    {
                        WithdrawContractAction?.Invoke(contractName);
                    }
                }

                if (_settings.WeatherPayload != WeatherPayload)
                {
                    foreach (string contractName in _weatherSatContracts)
                    {
                        WithdrawContractAction?.Invoke(contractName);
                    }
                }

                _settings.CommsPayload = CommsPayload;
                _settings.WeatherPayload = WeatherPayload;
            }
            finally
            {
                GUILayout.EndVertical();
            }

            GUILayout.BeginVertical();
            try
            {
                GUILayout.Space(10f);
                GUILayout.Space(10f);
                GUILayout.Label($"Use this area to change the name of the Newspaper Title.", BoldLabel);
                GUILayout.Space(10f);

                GUILayout.Label($"Newspaper Title: (18 characters max)", HighLogic.Skin.label, GUILayout.Width(250));
                NewspaperTitle = GUILayout.TextField(NewspaperTitle, HighLogic.Skin.textField);

                _settings.NewspaperTitle = NewspaperTitle;
            }
            finally
            {
                GUILayout.EndVertical();
            }
        }
    }
}
