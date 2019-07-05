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
            return $"{Math.Round(ControllableMass, 3)} t x {base.GetToolingParameterInfo()}";
        }

        public override float GetToolingCost()
        {
            GetDimensions(out var diameter, out var length, out var controllableMass);
            var toolingLevel = ToolingDatabase.GetToolingLevel(ToolingType, controllableMass, diameter, length);
            var toolingCosts = GetPerLevelToolingCosts(diameter, length);
            var toolingCost = 0f;
            for (int i = toolingLevel; i < 3; ++i)
            {
                toolingCost += toolingCosts[i];
            }

            return toolingCost;
        }

        private float[] GetPerLevelToolingCosts(float diameter, float length)
        {
            var avToolingFactor = 0.8f;
            var dimensionToolingFactor = 1 - avToolingFactor;
            var internalTankDiameter = diameter * Mathf.Sqrt(1 - procAvionics.Utilization);
            return new[] {
                GetControlledMassToolingCost() * avToolingFactor,
                base.GetDiameterToolingCost(diameter) * dimensionToolingFactor,
                base.GetLengthToolingCost(diameter, length) * dimensionToolingFactor + GetInternalTankToolingCosts(length, internalTankDiameter) * (1 - dimensionToolingFactor)
            };
        }

        private float GetInternalTankToolingCosts(float length, float internalTankDiameter) => (GetDiameterToolingCost(internalTankDiameter) + GetLengthToolingCost(internalTankDiameter, length));
        private float GetControlledMassToolingCost() => procAvionics.GetModuleCost(0, ModifierStagingSituation.UNSTAGED) * 20;

        public override void PurchaseTooling()
        {
            GetDimensions(out var diameter, out var length, out var controllableMass);
            ToolingDatabase.UnlockTooling(ToolingType, controllableMass, diameter, length);
        }

        public override bool IsUnlocked()
        {
            if(procAvionics == null)
            {
                return true;
            }
            GetDimensions(out var diameter, out var length, out var controllableMass);
            return ToolingDatabase.GetToolingLevel(ToolingType, controllableMass, diameter, length) == 3;
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
