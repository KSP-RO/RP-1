﻿using System.Collections.Generic;
using UnityEngine;

namespace RP0
{
    public class AvionicsGUI : UIBase
    {
        private double deltaTime = 0d;
        private const double UPDATEINTERVAL = 0.25d;
        private float maxMass, vesselMass;
        private bool haveParts = false, isControlLocked = false;

        public void Update()
        {
            deltaTime += Time.deltaTime;
            if (deltaTime > UPDATEINTERVAL)
            {
                deltaTime = 0;
                haveParts = false;
                isControlLocked = false;
                if (EditorLogic.fetch.ship?.Parts is List<Part> parts && parts.Count > 0)
                {
                    isControlLocked = ControlLockerUtils.ShouldLock(parts, false, out maxMass, out vesselMass);
                    haveParts = true;
                }
            }
        }

        public void avionicsTab()
        {
            Update();
            if (!haveParts)
                return;
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Supports:", HighLogic.Skin.label, GUILayout.Width(80));
                GUILayout.Label($"{maxMass:N3}t", rightLabel, GUILayout.Width(80));
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Vessel:", HighLogic.Skin.label, GUILayout.Width(80));
                GUILayout.Label($"{vesselMass:N3}t", rightLabel, GUILayout.Width(80));
            } finally {
                GUILayout.EndHorizontal();
            }
            if (isControlLocked)
                GUILayout.Label("Insufficient avionics!", boldLabel);
            else
                GUILayout.Label("Avionics are sufficient", HighLogic.Skin.label);
        }
    }
}

