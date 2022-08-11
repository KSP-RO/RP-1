using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace RP0
{
    public abstract class ModuleTooling : PartModule, IPartCostModifier
    {
        [KSPField] public string toolingType = string.Empty;
        [KSPField] public string costReducers = string.Empty;
        [KSPField] public float costReductionMult = 0.5f;
        [KSPField] public string toolingName = "Tool Part";
        [KSPField] public float untooledMultiplier = 0.25f;
        [KSPField] public float finalToolingCostMultiplier = 1;
        [KSPField] public float minDiameter = 0;
        [KSPField(isPersistant = true)] public float addedCost = 0;

        [KSPField] public Vector3 diameterToolingCost = new Vector3(3000f, 6000f, 250f); // d^2, d^1, 1
        [KSPField] public Vector4 lengthToolingCost = new Vector4(250f, 1000f, 100f, 50f); // d^2, d^1, l^1, 1

        protected BaseEvent tEvent;
        protected List<string> reducerList;
        protected bool onStartFinished;

        public virtual string ToolingType { get => toolingType; }

        public virtual List<string> CostReducers
        {
            get
            {
                reducerList ??= string.IsNullOrEmpty(costReducers)
                                ? new List<string>(0)
                                : new List<string>(costReducers.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                  .Select(s => s.Trim()));
                return reducerList;
            }
        }

        public virtual string GetToolingParameterInfo() => "";

        [KSPEvent(guiActiveEditor = true, guiName = "Tool Item")]
        public virtual void ToolingEvent()
        {
            if (IsUnlocked())
            {
                UpdateButtonName();
                return;
            }

            bool bypass = HighLogic.CurrentGame.Parameters.Difficulty.BypassEntryPurchaseAfterResearch;
            float toolingCost = bypass ? 0f : GetToolingCost();
            bool canAfford = CurrencyModifierQuery.RunQuery(TransactionReasonsRP0.ToolingPurchase.Stock(), -toolingCost, 0f, 0f).CanAfford();

            PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        new MultiOptionDialog(
                            "ConfirmToolingPurchase",
                            $"Tooling has not yet been set up for this part. It will cost { toolingCost:N0} funds.",
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
                                            using (new CareerEventScope(CareerEventType.Tooling)) 
                                            {
                                                Funding.Instance.AddFunds(-toolingCost, TransactionReasonsRP0.ToolingPurchase.Stock());
                                            }
                                            PurchaseTooling();
                                            GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
                                            UpdateButtonName();
                                        }
                                    }, 140.0f, 30.0f, true),
                                new DialogGUIButton("Close", () => { }, 140.0f, 30.0f, true)
                                )),
                        false,
                        HighLogic.UISkin);
        }

        private void UpdateButtonName() => tEvent.guiName = IsUnlocked() ? "Tooled" : toolingName;
        public abstract float GetToolingCost();

        public abstract void PurchaseTooling();

        public abstract bool IsUnlocked();

        public override void OnStart(StartState state)
        {
            Debug.Log($"[MT] OnStart() Loading part modules for {part}");
            tEvent = Events["ToolingEvent"];
            try
            {
                LoadPartModules();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            UpdateButtonName();
            onStartFinished = true;
        }

        protected virtual void LoadPartModules()
        {
        }

        public virtual float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
        {
            if (HighLogic.LoadedSceneIsEditor && HighLogic.CurrentGame.Mode == Game.Modes.CAREER && onStartFinished)
            {
                addedCost = GetUntooledPenaltyCost();
            }

            return addedCost;
        }

        protected float GetUntooledPenaltyCost()
        {
            UpdateButtonName();
            return IsUnlocked() ? 0 : GetToolingCost() * untooledMultiplier;
        }

        public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.FIXED;

        /// <summary>
        /// Use for purchasing multiple toolings at once. Does it's best to determine the best order to buy them so that the cost would be minimal.
        /// Also deducuts the funds required for purchase. 
        /// Has an option for running a simulation to calculate the accurate cost of toolings while the tooling DB and funds are left untouched.
        /// </summary>
        /// <param name="toolingColl">Collection of toolings to purchase.</param>
        /// <param name="isSimulation">Whether to simulate the purchase to get the accurate cost of all toolings.</param>
        /// <returns>Total cost of all the toolings purchased.</returns>
        public static float PurchaseToolingBatch(List<ModuleTooling> toolingColl, bool isSimulation = false)
        {
            ConfigNode toolingBackup = null;
            if (isSimulation)
            {
                toolingBackup = new ConfigNode();
                ToolingDatabase.Save(toolingBackup);
            }

            float totalCost = 0;
            try
            {
                // Currently all cost reducers are applied correctly when the tooling types are first sorted in alphabetical order
                toolingColl.Sort((mt1, mt2) => mt1.ToolingType.CompareTo(mt2.ToolingType));

                //TODO: find the most optimal order to purchase toolings that have diameter and length.
                //      If there are diameters 2.9; 3 and 3.1 only 3 needs to be purchased and others will fit inside the stretch margin.

                toolingColl.ForEach(mt =>
                {
                    if (mt.IsUnlocked()) return;

                    totalCost += mt.GetToolingCost();
                    mt.PurchaseTooling();
                });

                if (totalCost > 0 && !isSimulation)
                {
                    using (new CareerEventScope(CareerEventType.Tooling))
                    {
                        Funding.Instance.AddFunds(-totalCost, TransactionReasonsRP0.ToolingPurchase.Stock());
                    }
                }
            }
            finally
            {
                if (isSimulation)
                {
                    ToolingDatabase.Load(toolingBackup);
                }
            }

            return totalCost;
        }

        /// <summary>
        /// Checks whether two toolings are considered the same by checking their types and dimensions (if applicable).
        /// Dimension comparison is done with an error margin of 4%.
        /// </summary>
        /// <param name="a">Tooling 1</param>
        /// <param name="b">Tooling 2</param>
        /// <returns>True if toolings match</returns>
        public static bool IsSame(ModuleTooling a, ModuleTooling b)
        {
            if (a.ToolingType != b.ToolingType) return false;

            if (a is ModuleToolingDiamLen || b is ModuleToolingDiamLen)
            {
                var d1 = a as ModuleToolingDiamLen;
                var d2 = b as ModuleToolingDiamLen;
                if (d1 == null || d2 == null) return false;

                d1.GetDimensions(out float diam1, out float len1);
                d2.GetDimensions(out float diam2, out float len2);

                return ToolingDatabase.IsSameSize(diam1, len1, diam2, len2);
            }

            return true;
        }
    }
}
