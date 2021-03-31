﻿using System.Collections.Generic;
using UnityEngine;

namespace RP0
{
    public class AvionicsGUI : UIBase
    {
        private const double UpdateInterval = 0.25;
        private double _deltaTime = 0;
        private float _maxMass, _vesselMass;
        private bool _haveParts = false, _isControlLocked = false;

        public void Update()
        {
            _deltaTime += Time.deltaTime;
            if (_deltaTime > UpdateInterval)
            {
                _deltaTime = 0;
                _haveParts = false;
                _isControlLocked = false;
                if (EditorLogic.fetch.ship?.Parts is List<Part> parts && parts.Count > 0)
                {
                    _isControlLocked = ControlLockerUtils.ShouldLock(parts, false, out _maxMass, out _vesselMass);
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
            GUILayout.Label("Supports:", HighLogic.Skin.label, GUILayout.Width(80));
            GUILayout.Label($"{_maxMass:N3}t", RightLabel, GUILayout.Width(80));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Vessel:", HighLogic.Skin.label, GUILayout.Width(80));
            GUILayout.Label($"{_vesselMass:N3}t", RightLabel, GUILayout.Width(80));
            GUILayout.EndHorizontal();

            if (_isControlLocked)
                GUILayout.Label("Insufficient avionics!", BoldLabel);
            else
                GUILayout.Label("Avionics are sufficient", HighLogic.Skin.label);
        }
    }
}
