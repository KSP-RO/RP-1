using System.Collections.Generic;
using UnityEngine;

namespace RP0
{
    public class AvionicsGUI : UIBase
    {
        private const double UpdateInterval = 0.25;
        private double _deltaTime = 0;
        private float _maxMass, _vesselMass;
        private bool _haveParts = false;
        ControlLockerUtils.LockLevel _lockLevel = ControlLockerUtils.LockLevel.Unlocked;

        public void Update()
        {
            _deltaTime += Time.deltaTime;
            if (_deltaTime > UpdateInterval)
            {
                _deltaTime = 0;
                _haveParts = false;
                _lockLevel = ControlLockerUtils.LockLevel.Unlocked;
                if (EditorLogic.fetch.ship?.Parts is List<Part> parts && parts.Count > 0)
                {
                    _lockLevel = ControlLockerUtils.ShouldLock(parts, false, out _maxMass, out _vesselMass, out _);
                    _haveParts = true;
                }
            }
        }

        public void RenderAvionicsTab()
        {
            Update();
            if (!_haveParts)
                return;

            GUILayout.BeginHorizontal();
            GUILayout.Label("Supports:", UIHolder.Width(80));
            GUILayout.Label($"{_maxMass:N3}t", RightLabel, UIHolder.Width(80));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Vessel:", UIHolder.Width(80));
            GUILayout.Label($"{_vesselMass:N3}t", RightLabel, UIHolder.Width(80));
            GUILayout.EndHorizontal();

            if (_lockLevel != ControlLockerUtils.LockLevel.Unlocked)
                GUILayout.Label("Insufficient avionics!", BoldLabel);
            else
                GUILayout.Label("Avionics are sufficient");
        }
    }
}
