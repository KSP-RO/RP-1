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

        private RP0Settings _settings;

        protected override void OnStart()
        {
            _settings = HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>();
            CommsPayload = _settings.CommsPayload;
            WeatherPayload = _settings.WeatherPayload;
        }

        public void RenderContractsTab()
        {
            GUILayout.BeginVertical();
            try
            {
                GUILayout.Label($"Use this tab to set the required payload amount for contracts.", BoldLabel);
                UIHolder.Space(10f);

                GUILayout.Label($"CommSat Payload range: {Math.Max(CommsPayload / 2, 300)} - {CommsPayload}", UIHolder.Width(250));
                float commsAmnt = GUILayout.HorizontalSlider(CommsPayload, MinPayload, MaxPayload);
                CommsPayload = Mathf.RoundToInt(commsAmnt / 100) * 100;    // slider works in increments of 100
                UIHolder.Space(5f);
                GUILayout.Label($"WeatherSat Payload range: {Math.Max(WeatherPayload / 2, 300)} - {WeatherPayload}", UIHolder.Width(250));
                float weatherAmnt = GUILayout.HorizontalSlider(WeatherPayload, MinPayload, MaxPayload);
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
        }
    }
}
