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
        private ModuleProceduralAvionics _procAvionics;

        public override string ToolingType => $"{MainToolingType}-{_procAvionics.CurrentProceduralAvionicsConfig.name[0]}{_procAvionics.CurrentProceduralAvionicsTechNode.techLevel}";
        private string TankToolingType => base.ToolingType;
        private ToolingDefinition TankToolingDefinition => ToolingManager.Instance.GetToolingDefinition(TankToolingType);

        private float ControllableMass => _procAvionics?.controllableMass ?? 0f;

        protected override void LoadPartModules()
        {
            base.LoadPartModules();
            _procAvionics = part.Modules.GetModule<ModuleProceduralAvionics>();
        }

        public override string GetToolingParameterInfo()
        {
            return $"{Math.Round(ControllableMass, 3)} t x {base.GetToolingParameterInfo()}{GetInternalTankDiameterInfo()}";
        }

        private object GetInternalTankDiameterInfo()
        {
            if (_procAvionics.InternalTanksVolume == 0)
            {
                return "";
            }

            GetDimensions(out var diameter, out var length);
            var tankDiameter = GetInternalTankDiameter(diameter, length);
            return $" ({TankToolingType} {tankDiameter} m)";
        }

        public override float GetToolingCost()
        {
            GetDimensions(out var diameter, out var length);
            return GetAvionicsToolingCost(diameter, length) + GetInternalTankToolingCost(diameter, length);
        }

        private float GetAvionicsToolingCost(float diameter, float length)
        {
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
                base.GetLengthToolingCost(diameter, length) * dimensionToolingFactor
            };
        }

        private float GetInternalTankToolingCost(float externalDiameter, float length)
        {
            if(_procAvionics.InternalTanksVolume == 0)
            {
                return 0;
            }

            var internalTankDiameter = GetInternalTankDiameter(externalDiameter, length);
            var level = ToolingDatabase.GetToolingLevel(TankToolingType, internalTankDiameter, internalTankDiameter);
            var perLevelCosts = new[] { GetDiameterToolingCost(internalTankDiameter), GetLengthToolingCost(internalTankDiameter, internalTankDiameter) };
            var costMult = TankToolingDefinition?.finalToolingCostMultiplier ?? 0f;
            return GetToolingCost(level, perLevelCosts) * costMult;
        }

        private float GetInternalTankDiameter(float externalDiameter, float length)
        {
            var maxDiameter = Mathf.Min(externalDiameter * 2 / 3, length);
            var internalTankDiameter = SphericalTankUtilities.GetSphericalTankRadius(_procAvionics.InternalTanksVolume) * 2;
            while (internalTankDiameter > maxDiameter) { internalTankDiameter /= 2; }

            return internalTankDiameter;
        }

        private float GetControlledMassToolingCost() => _procAvionics.GetModuleCost(0, ModifierStagingSituation.UNSTAGED) * avionicsToolingCostMultiplier;

        public override float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
        {
            if (!onStartFinished) return 0f;

            return GetUntooledPenaltyCost() + GetInternalTankModuleCost();
        }

        private float GetInternalTankModuleCost()
        {
            if (_procAvionics.InternalTanksVolume == 0)
            {
                return 0;
            }

            GetDimensions(out var externalDiameter, out var length);
            var internalTankDiameter = GetInternalTankDiameter(externalDiameter, length);
            var tankCount = _procAvionics.InternalTanksVolume / SphericalTankUtilities.GetSphereVolume(internalTankDiameter / 2);
            var costMultDL = TankToolingDefinition?.costMultiplierDL ?? 0f;

            return GetDimensionModuleCost(internalTankDiameter, length, costMultDL) * tankCount;>>>>>>> upstream/master
        }

        public override void PurchaseTooling()
        {
            GetDimensions(out var diameter, out var length);
            ToolingDatabase.UnlockTooling(ToolingType, ControllableMass, diameter, length);
            UnlockInternalTankTooling(diameter, length);
        }

        private void UnlockInternalTankTooling(float diameter, float length)
        {
            if(_procAvionics.InternalTanksVolume == 0)
            {
                return;
            }

            var internalTankDiameter = GetInternalTankDiameter(diameter, length);
            ToolingDatabase.UnlockTooling(TankToolingType, internalTankDiameter, internalTankDiameter);
        }

        public override bool IsUnlocked()
        {
            if (_procAvionics == null)
            {
                return true;
            }

            GetDimensions(out var diameter, out var length);
            return IsAvionicsTooled(diameter, length) && IsInternalTankTooled(diameter, length);
        }

        private bool IsInternalTankTooled(float diameter, float length)
        {
            if(_procAvionics.InternalTanksVolume == 0)
            {
                return true;
            }

            var internalTankDiameter = GetInternalTankDiameter(diameter, length);
            return ToolingDatabase.GetToolingLevel(TankToolingType, internalTankDiameter, internalTankDiameter) == 2;
        }

        private bool IsAvionicsTooled(float diameter, float length) => ToolingDatabase.GetToolingLevel(ToolingType, ControllableMass, diameter, length) == 3;
    }
}
