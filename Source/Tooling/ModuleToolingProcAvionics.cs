using System;
using RP0.ProceduralAvionics;
using UnityEngine;

namespace RP0
{
    public class ModuleToolingProcAvionics : ModuleToolingPTank
    {
        [KSPField]
        public float avionicsToolingCostMultiplier = 10f;

        public const string MainToolingType = "Avionics";
        private ModuleProceduralAvionics procAvionics;

        public override string ToolingType => $"{MainToolingType}-{procAvionics.CurrentProceduralAvionicsConfig.name[0]}{procAvionics.CurrentProceduralAvionicsTechNode.techLevel}-{base.ToolingType}";

        private ToolingDefinition TankToolingDefinition => ToolingManager.Instance.GetToolingDefinition(base.ToolingType);

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
            var controlledMassToolingFactor = 0.95f;
            var dimensionToolingFactor = 1 - controlledMassToolingFactor;
            return new[] {
                GetControlledMassToolingCost() * controlledMassToolingFactor,
                base.GetDiameterToolingCost(diameter) * dimensionToolingFactor,
                base.GetLengthToolingCost(diameter, length) * dimensionToolingFactor + GetInternalTankToolingCosts(diameter, length)
            };
        }

        private float GetInternalTankToolingCosts(float externalDiameter, float length)
        {
            //simulate the use of 7 cylindrical tanks, each having a diameter of 1/3 of the surrounding cylinder
            var internalTankDiameter = GetInternalTankDiameter(externalDiameter);
            return (GetDiameterToolingCost(internalTankDiameter) + GetLengthToolingCost(internalTankDiameter, length)) * TankToolingDefinition.finalToolingCostMultiplier;
        }

        private float GetInternalTankDiameter(float externalDiameter) => externalDiameter * Mathf.Sqrt(1 - procAvionics.Utilization) / 3;
        private float GetControlledMassToolingCost() => procAvionics.GetModuleCost(0, ModifierStagingSituation.UNSTAGED) * avionicsToolingCostMultiplier;

        public override float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
        {
            if (!onStartFinished) return 0f;

            return GetUntooledPenaltyCost() + GetInternalTankModuleCost();
        }

        private float GetInternalTankModuleCost()
        {
            GetDimensions(out var diameter, out var length, out _);
            var tankToolingDef = TankToolingDefinition;
            var internalTankDiameter = GetInternalTankDiameter(diameter);
            return GetDimensionModuleCost(internalTankDiameter, length, tankToolingDef.costMultiplierDL);
        }

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
