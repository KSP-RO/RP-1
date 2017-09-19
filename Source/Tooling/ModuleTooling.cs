using System;
using System.Collections.Generic;
using System.Text;
using KSP;
using UnityEngine;

namespace RP0
{
    public abstract class ModuleTooling : PartModule, IPartCostModifier
    {
        [KSPField]
        public string toolingType = "TankStarting";

        [KSPField]
        public string costReducers = string.Empty;

        [KSPField]
        public float costReductionMult = 0.5f;

        public List<string> reducers = new List<string>();

        [KSPField]
        public string toolingName = "Tool Tank";

        [KSPField]
        public float untooledMultiplier = 0.25f;

        [KSPField]
        public float finalToolingCostMultiplier = 1f;

        [KSPField]
        // d^2, d^1, 1
        public Vector3 diameterToolingCost = new Vector3(3000f, 6000f, 250f);

        [KSPField]
        // d^2, d^1, l^1, 1
        public Vector4 lengthToolingCost = new Vector4(250f, 1000f, 100f, 50f);

        [KSPField]
        public float minDiameter = 0f;

        protected BaseEvent tEvent;

        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "Tool Item")]
        public virtual void ToolingEvent()
        {
            if (IsUnlocked())
            {
                tEvent.guiActiveEditor = false;
                return;
            }

            float toolingCost = GetToolingCost();
            bool canAfford = true;
            if (!HighLogic.CurrentGame.Parameters.Difficulty.BypassEntryPurchaseAfterResearch)
            {
                if (Funding.Instance.Funds < toolingCost)
                    canAfford = false;
            }
            else
                toolingCost = 0f;

            PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        new MultiOptionDialog(
                            "ConfirmToolingPurchase",
                            "Tooling has not yet been set up for this part. It will cost " + toolingCost.ToString("N0") + " funds.",
                            "Tooling Purchase",
                            HighLogic.UISkin,
                            new Rect(0.5f, 0.5f, 150f, 60f),
                            new DialogGUIFlexibleSpace(),
                            new DialogGUIVerticalLayout(
                                new DialogGUIFlexibleSpace(),
                                new DialogGUIButton(canAfford ? "Purchase Tooling" : "Can't Afford",
                                    () =>
                                    {
                                        if (canAfford)
                                        {
                                            Funding.Instance.AddFunds(-toolingCost, TransactionReasons.RnDPartPurchase);
                                            PurchaseTooling();
                                            GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
                                            Events["ToolingEvent"].guiActiveEditor = false;
                                        }
                                    }, 140.0f, 30.0f, true),
                                new DialogGUIButton("Close", () => { }, 140.0f, 30.0f, true)
                                )),
                        false,
                        HighLogic.UISkin);
        }

        public abstract float GetToolingCost();

        public abstract void PurchaseTooling();

        public abstract bool IsUnlocked();

        public override void OnAwake()
        {
            base.OnAwake();

            tEvent = Events["ToolingEvent"];
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            tEvent = Events["ToolingEvent"];

            tEvent.guiActiveEditor = IsUnlocked();
            tEvent.guiName = toolingName;

            if (!string.IsNullOrEmpty(costReducers))
                reducers = new List<string>(costReducers.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
        }

        public virtual float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
        {
            if (!HighLogic.LoadedSceneIsEditor)
                return 0f;

            if (IsUnlocked())
            {
                tEvent.guiActiveEditor = false;
                return 0f;
            }
            tEvent.guiActiveEditor = true;

            return GetToolingCost() * untooledMultiplier;
        }

        public ModifierChangeWhen GetModuleCostChangeWhen()
        {
            return ModifierChangeWhen.FIXED;
        }
    }
}
