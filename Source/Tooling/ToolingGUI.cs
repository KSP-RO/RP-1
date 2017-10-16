using System;
using System.Collections.Generic;
using UnityEngine;

namespace RP0
{
    public class ToolingGUI : UIBase
    {
        private struct untooledPart {
            public string name;
            public float toolingCost;
            public float untooledMultiplier;
        };
        private Vector2 toolingTypesScroll = new Vector2(), untooledTypesScroll = new Vector2();
        private double deltaTime = 0d;
        private const double UPDATEINTERVAL = 0.25d;
        private static HashSet<untooledPart> untooledParts = new HashSet<untooledPart>();
        public string currentToolingType;

        private void MaybeUpdate()
        {
            if (!HighLogic.LoadedSceneIsEditor)
                return;
            deltaTime += Time.deltaTime;
            if (deltaTime > UPDATEINTERVAL) {
                deltaTime = 0;
                Update();
            }
        }

        private void Update()
        {
            untooledParts.Clear();
            if (EditorLogic.fetch != null && EditorLogic.fetch.ship != null && EditorLogic.fetch.ship.Parts.Count > 0) {
                for (int i = EditorLogic.fetch.ship.Parts.Count; i-- > 0;) {
                    Part p = EditorLogic.fetch.ship.Parts[i];
                    for (int j = p.Modules.Count; j-- > 0;) {
                        PartModule m = p.Modules[j];
                        ModuleTooling mT;
                        if (m is ModuleTooling && !((mT = (m as ModuleTooling)).IsUnlocked())) {
                            untooledPart uP;
                            if (m is ModuleToolingDiamLen) {
                                uP.name = p.partInfo.title + " (" + mT.toolingType + ") " + (m as ModuleToolingDiamLen).GetDimensions();
                            } else {
                                uP.name = p.partInfo.title + " (" + mT.toolingType + ")";
                            }
                            uP.toolingCost = mT.GetToolingCost();
                            uP.untooledMultiplier = mT.untooledMultiplier;
                            untooledParts.Add(uP);
                        }
                    }
                }
            }
        }

        public tabs toolingTab()
        {
            MaybeUpdate();
            currentToolingType = null;
            GUILayout.BeginHorizontal();
            try {
                GUILayout.FlexibleSpace();
                GUILayout.Label("Tooling Types", HighLogic.Skin.label);
                GUILayout.FlexibleSpace();
            } finally {
                GUILayout.EndHorizontal();
            }
            int counter = 0;
            GUILayout.BeginHorizontal();
            try {
                foreach (string type in ToolingDatabase.toolings.Keys) {
                    if (counter % 3 == 0 && counter != 0) {
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                    }
                    counter++;
                    if (GUILayout.Button(type))
                        currentToolingType = type;
                }
            } finally {
                GUILayout.EndHorizontal();
            }

            if (untooledParts.Count > 0) {
                GUILayout.BeginHorizontal();
                try {
                    GUILayout.Label("Untooled Parts:", HighLogic.Skin.label, GUILayout.Width(312));
                    GUILayout.Label("Tooling cost", rightLabel, GUILayout.Width(72));
                    GUILayout.Label("Untooled", rightLabel, GUILayout.Width(72));
                } finally {
                    GUILayout.EndHorizontal();
                }
                untooledTypesScroll = GUILayout.BeginScrollView(untooledTypesScroll, GUILayout.Height(144), GUILayout.Width(500));
                try {
                    foreach (untooledPart uP in untooledParts) {
                        GUILayout.BeginHorizontal();
                        try {
                            GUILayout.Label(uP.name, boldLabel, GUILayout.Width(312));
                            GUILayout.Label(uP.toolingCost.ToString("N0") + "f", rightLabel, GUILayout.Width(72));
                            float untooledCost = uP.toolingCost * uP.untooledMultiplier;
                            GUILayout.Label(untooledCost.ToString("N0") + "f", rightLabel, GUILayout.Width(72));
                        } finally {
                            GUILayout.EndHorizontal();
                        }
                    }
                } finally {
                    GUILayout.EndScrollView();
                }
            }
            return currentToolingType == null ? tabs.Tooling : tabs.ToolingType;
        }

        private void toolingTypesHeading()
        {
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Diameter", HighLogic.Skin.label, GUILayout.Width(80));
                GUILayout.Label("×", HighLogic.Skin.label);
                GUILayout.Label("Length", rightLabel, GUILayout.Width(80));
            } finally {
                GUILayout.EndHorizontal();
            }
        }

        private void toolingTypeRow(float diameter, float length)
        {
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label(diameter.ToString("F2") + "m", HighLogic.Skin.label, GUILayout.Width(80));
                GUILayout.Label("×", HighLogic.Skin.label);
                GUILayout.Label(length.ToString("F2") + "m", rightLabel, GUILayout.Width(80));
            } finally {
                GUILayout.EndHorizontal();
            }
        }

        public void toolingTypeTab()
        {
            GUILayout.BeginHorizontal();
            try {
                GUILayout.FlexibleSpace();
                GUILayout.Label("Toolings for type "+currentToolingType, HighLogic.Skin.label);
                GUILayout.FlexibleSpace();
            } finally {
                GUILayout.EndHorizontal();
            }
            toolingTypesHeading();
            toolingTypesScroll = GUILayout.BeginScrollView(toolingTypesScroll, GUILayout.Width(200), GUILayout.Height(240));
            try {
                foreach (ToolingDiameter td in ToolingDatabase.toolings[currentToolingType]) {
                    foreach (float length in td.lengths) {
                        toolingTypeRow(td.diameter, length);
                    }
                }
            } finally {
                GUILayout.EndScrollView();
            }
        }
    }
}

