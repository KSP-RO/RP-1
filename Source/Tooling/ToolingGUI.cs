using System.Collections.Generic;
using UnityEngine;
using Smooth.Slinq;

namespace RP0
{
    public class ToolingGUI : UIBase
    {
        private struct untooledPart {
            public string name;
            public float toolingCost;
            public float untooledMultiplier;
            public float totalCost;
        };

        public string currentToolingType;

        private const float UPDATEINTERVAL = 0.5f;

        private float nextUpdate = 0f;
        private float allTooledCost;
        private List<untooledPart> untooledParts = new List<untooledPart>();
        private Vector2 toolingTypesScroll = new Vector2(), untooledTypesScroll = new Vector2();

        private void MaybeUpdate()
        {
            if (!HighLogic.LoadedSceneIsEditor)
                return;

            if (Time.time > nextUpdate)
            {
                nextUpdate = Time.time + UPDATEINTERVAL;
                Update();
            }
        }

        private void Update()
        {
            untooledParts.Clear();
            float totalUntooledExtraCost = 0;

            if (EditorLogic.fetch != null && EditorLogic.fetch.ship != null && EditorLogic.fetch.ship.Parts.Count > 0) {
                for (int i = EditorLogic.fetch.ship.Parts.Count; i-- > 0;) {
                    Part p = EditorLogic.fetch.ship.Parts[i];
                    for (int j = p.Modules.Count; j-- > 0;) {
                        PartModule m = p.Modules[j];
                        ModuleTooling mT;
                        if (m is ModuleTooling && !((mT = (m as ModuleTooling)).IsUnlocked())) {
                            untooledPart uP;
                            if (m is ModuleToolingDiamLen) {
                                uP.name = $"{p.partInfo.title} ({mT.ToolingType}) {(m as ModuleToolingDiamLen).GetDimensions()}";
                            } else {
                                uP.name = $"{p.partInfo.title} ({mT.ToolingType})";
                            }
                            uP.toolingCost = mT.GetToolingCost();
                            uP.untooledMultiplier = mT.untooledMultiplier;
                            uP.totalCost = p.GetModuleCosts(p.partInfo.cost) + p.partInfo.cost;
                            totalUntooledExtraCost += GetUntooledExtraCost(uP);
                            untooledParts.Add(uP);
                        }
                    }
                }
            }

            allTooledCost = EditorLogic.fetch.ship.GetShipCosts(out _, out _) - totalUntooledExtraCost;
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
                    GUILayout.Label("Tooled", rightLabel, GUILayout.Width(72));
                } finally {
                    GUILayout.EndHorizontal();
                }
                untooledTypesScroll = GUILayout.BeginScrollView(untooledTypesScroll, GUILayout.Height(204), GUILayout.Width(572));
                try {
                    foreach (untooledPart uP in untooledParts) {
                        GUILayout.BeginHorizontal();
                        try
                        {
                            GUILayout.Label(uP.name, boldLabel, GUILayout.Width(312));
                            GUILayout.Label($"{uP.toolingCost:N0}f", rightLabel, GUILayout.Width(72));
                            var untooledExtraCost = GetUntooledExtraCost(uP);
                            GUILayout.Label($"{uP.totalCost:N0}f", rightLabel, GUILayout.Width(72));
                            GUILayout.Label($"{(uP.totalCost - untooledExtraCost):N0}f", rightLabel, GUILayout.Width(72));
                        }
                        finally {
                            GUILayout.EndHorizontal();
                        }
                    }
                } finally {
                    GUILayout.EndScrollView();
                }
                GUILayout.BeginHorizontal();
                try {
                    GUILayout.Label($"Total vessel cost if all parts are tooled: {allTooledCost:N0}");
                } finally {
                    GUILayout.EndHorizontal();
                }
                GUILayout.BeginHorizontal();
                try {
                if (GUILayout.Button("Tool All"))
                {
                    var untooledParts = EditorLogic.fetch.ship.Parts.Slinq().SelectMany(p => p.FindModulesImplementing<ModuleTooling>().Slinq())
                                                                            .Where(mt => !mt.IsUnlocked())
                                                                            .ToList();

                    float totalToolingCost = ModuleTooling.PurchaseToolingBatch(untooledParts, isSimulation: true);
                    bool canAfford = Funding.Instance.Funds >= totalToolingCost;
                    PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        new MultiOptionDialog(
                            "ConfirmAllToolingsPurchase",
                            $"Tooling for all untooled parts will cost {totalToolingCost:N0} funds.",
                            "Tooling Purchase",
                            HighLogic.UISkin,
                            new Rect(0.5f, 0.5f, 150f, 60f),
                            new DialogGUIFlexibleSpace(),
                            new DialogGUIVerticalLayout(
                                new DialogGUIFlexibleSpace(),
                                new DialogGUIButton(canAfford ? "Purchase All Toolings" : "Can't Afford",
                                    () =>
                                    {
                                        if (canAfford)
                                        {
                                            ModuleTooling.PurchaseToolingBatch(untooledParts);
                                            untooledParts.ForEach(mt =>
                                            {
                                                mt.Events["ToolingEvent"].guiActiveEditor = false;
                                            });
                                            GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
                                        }
                                    }, 140.0f, 30.0f, true),
                                new DialogGUIButton("Close", () => { }, 140.0f, 30.0f, true)
                                )),
                        false,
                        HighLogic.UISkin);
                }
                } finally {
                    GUILayout.EndHorizontal();
                }
            }
            return currentToolingType == null ? tabs.Tooling : tabs.ToolingType;
        }

        private static float GetUntooledExtraCost(untooledPart uP)
        {
            return uP.toolingCost * uP.untooledMultiplier;
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
            try
            {
                GUILayout.Label($"{diameter:F3}m", HighLogic.Skin.label, GUILayout.Width(80));
                GUILayout.Label("×", HighLogic.Skin.label);
                GUILayout.Label($"{length:F3}m", rightLabel, GUILayout.Width(80));
            } finally {
                GUILayout.EndHorizontal();
            }
        }

        public void toolingTypeTab()
        {
            GUILayout.BeginHorizontal();
            try {
                GUILayout.FlexibleSpace();
                GUILayout.Label($"Toolings for type {currentToolingType}", HighLogic.Skin.label);
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

