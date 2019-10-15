using System;
using RP0.ProceduralAvionics;
using RP0.Utilities;
using UnityEngine;

namespace RP0
{
    public class ModuleToolingProcAvionics : ModuleToolingPTank
    {
        [KSPField]
        public float avionicsToolingCostMultiplier = 5f;

        public const string MainToolingType = "Avionics";
        private ModuleProceduralAvionics procAvionics;

        public override string ToolingType => $"{MainToolingType}-{procAvionics.CurrentProceduralAvionicsConfig.name[0]}{procAvionics.CurrentProceduralAvionicsTechNode.techLevel}-{base.ToolingType}";
        private string TankToolingType => base.ToolingType;
        private ToolingDefinition TankToolingDefinition => ToolingManager.Instance.GetToolingDefinition(TankToolingType);

        private float ControllableMass => procAvionics?.controllableMass ?? 0f;

        protected override void LoadPartModules()
        {
            base.LoadPartModules();
            procAvionics = part.Modules.GetModule<ModuleProceduralAvionics>();
        }

        public override string GetToolingParameterInfo()
        {
            return $"{Math.Round(ControllableMass, 3)} t x {base.GetToolingParameterInfo()}";
        }

        public override float GetToolingCost()
        {
            GetDimensions(out var diameter, out var length);
            var toolingLevel = ToolingDatabase.GetToolingLevel(ToolingType, ControllableMass, diameter, length);
            var toolingCosts = GetPerLevelToolingCosts(diameter, length);

            return GetToolingCost(toolingLevel, toolingCosts);
        }

        private static float GetToolingCost(int toolingLevel, float[] toolingCosts)
        {
            var toolingCost = 0f;
            for (int i = toolingLevel; i < toolingCosts.Length; ++i)
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
            if(procAvionics.InternalTanksVolume == 0)
            {
                return 0;
            }

            var internalTankDiameter = GetInternalTankDiameter(externalDiameter, length);
            var level = ToolingDatabase.GetToolingLevel(TankToolingType, internalTankDiameter, internalTankDiameter);
            var perLevelCosts = new[] { GetDiameterToolingCost(internalTankDiameter), GetLengthToolingCost(internalTankDiameter, internalTankDiameter) };
            return GetToolingCost(level, perLevelCosts) * TankToolingDefinition.finalToolingCostMultiplier;
        }

        private float GetInternalTankDiameter(float externalDiameter, float length)
        {
            var maxDiameter = Mathf.Min(externalDiameter * 2 / 3, length);
            var internalTankDiameter = SphericalTankUtilities.GetSphericalTankRadius(procAvionics.InternalTanksVolume) * 2;
            while (internalTankDiameter > maxDiameter) { internalTankDiameter /= 2; }

            return internalTankDiameter;
        }

        private float GetControlledMassToolingCost() => procAvionics.GetModuleCost(0, ModifierStagingSituation.UNSTAGED) * avionicsToolingCostMultiplier;

        public override float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
        {
            if (!onStartFinished) return 0f;

            return GetUntooledPenaltyCost() + GetInternalTankModuleCost();
        }

        private float GetInternalTankModuleCost()
        {
            if (procAvionics.InternalTanksVolume == 0)
            {
                return 0;
            }

            GetDimensions(out var externalDiameter, out var length);
            var internalTankDiameter = GetInternalTankDiameter(externalDiameter, length);
            var tankCount = procAvionics.InternalTanksVolume / SphericalTankUtilities.GetSphereVolume(internalTankDiameter / 2);
            
            return GetDimensionModuleCost(internalTankDiameter, length, TankToolingDefinition.costMultiplierDL) * tankCount;
        }

        public override void PurchaseTooling()
        {
            GetDimensions(out var diameter, out var length);
            ToolingDatabase.UnlockTooling(ToolingType, ControllableMass, diameter, length);
            var internalTankDiameter = GetInternalTankDiameter(diameter, length);
            ToolingDatabase.UnlockTooling(TankToolingType, internalTankDiameter, internalTankDiameter);
        }

        public override bool IsUnlocked()
        {
            if(procAvionics == null)
            {
                return true;
            }

            GetDimensions(out var diameter, out var length);
            return ToolingDatabase.GetToolingLevel(ToolingType, ControllableMass, diameter, length) == 3;
        }
    }
}
