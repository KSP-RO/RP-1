using System;
using RP0.ProceduralAvionics;
using UnityEngine;

namespace RP0
{
    public class ModuleToolingProcAvionics : ModuleToolingPTank
    {
        public const string MainToolingType = "Avionics";
        private ModuleProceduralAvionics procAvionics;

        public override string ToolingType => $"{MainToolingType}-{procAvionics.CurrentProceduralAvionicsConfig.name[0]}{procAvionics.CurrentProceduralAvionicsTechNode.techLevel}-{base.ToolingType}";

        protected override void LoadPartModules()
        {
            Debug.Log("[AvionicsTooling] Loading part modules");
            base.LoadPartModules();
            procAvionics = part.Modules.GetModule<ModuleProceduralAvionics>();
        }

        public override string GetToolingParameterInfo()
        {
            return base.GetToolingParameterInfo() + $" x {Math.Round(ControllableMass, 3)} t";
        }

        public override float GetToolingCost()
        {
            GetDimensions(out var diameter, out var length, out var controllableMass);
            var toolingLevel = ToolingDatabase.GetToolingLevel(ToolingType, diameter, length, controllableMass);
            var baseCost = toolingLevel < 3 ? base.GetToolingCost() / finalToolingCostMultiplier : 0;

            return (baseCost + procAvionics.GetModuleCost(0, ModifierStagingSituation.UNSTAGED) * 15) * finalToolingCostMultiplier;
        }

        public override void PurchaseTooling()
        {
            GetDimensions(out var diameter, out var length, out var controllableMass);
            ToolingDatabase.UnlockTooling(ToolingType, diameter, length, controllableMass);
        }

        public override bool IsUnlocked()
        {
            if(procAvionics == null)
            {
                return true;
            }
            GetDimensions(out var diameter, out var length, out var controllableMass);
            return ToolingDatabase.GetToolingLevel(ToolingType, diameter, length, controllableMass) == 3;
        }

        private void GetDimensions(out float diameter, out float length, out float controllableMass)
        {
            if(procAvionics == null)
            {
                diameter = length = controllableMass = 0;
                return;
            }

            GetDimensions(out diameter, out length);
            controllableMass = ControllableMass;
        }

        private float ControllableMass => procAvionics.controllableMass;
    }
}
