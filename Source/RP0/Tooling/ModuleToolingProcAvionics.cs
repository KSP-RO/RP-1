using System;
using RP0.ProceduralAvionics;

namespace RP0
{
    public class ModuleToolingProcAvionics : ModuleToolingPTank
    {
        [KSPField]
        public float avionicsToolingCostMultiplier = 5f;

        public const string MainToolingType = "Avionics";
        private ModuleProceduralAvionics _procAvionics;

        public override string ToolingType => $"{MainToolingType}-{_procAvionics.CurrentProceduralAvionicsConfig.name[0]}{_procAvionics.CurrentProceduralAvionicsTechNode.techLevel}";

        private float ControllableMass => _procAvionics?.controllableMass ?? 0f;

        protected override void LoadPartModules()
        {
            base.LoadPartModules();
            _procAvionics = part.Modules.GetModule<ModuleProceduralAvionics>();
        }

        public override string GetToolingParameterInfo()
        {
            return $"{Math.Round(ControllableMass, 3)} t x {base.GetToolingParameterInfo()}";
        }

        public override float GetToolingCost()
        {
            GetDimensions(out var diameter, out var length);
            return GetAvionicsToolingCost(diameter, length);
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

        private float GetControlledMassToolingCost() => _procAvionics.GetModuleCost(0, ModifierStagingSituation.UNSTAGED) * avionicsToolingCostMultiplier;

        public override float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
        {
            if (!onStartFinishedFinished || HighLogic.CurrentGame.Mode != Game.Modes.CAREER) return 0f;

            return GetUntooledPenaltyCost();
        }

        public override void PurchaseTooling()
        {
            GetDimensions(out var diameter, out var length);
            ToolingDatabase.UnlockTooling(ToolingType, ControllableMass, diameter, length);
        }

        public override bool IsUnlocked()
        {
            if (_procAvionics == null)
            {
                return true;
            }

            GetDimensions(out var diameter, out var length);
            return IsAvionicsTooled(diameter, length);
        }

        private bool IsAvionicsTooled(float diameter, float length) => ToolingDatabase.GetToolingLevel(ToolingType, ControllableMass, diameter, length) == 3;
    }
}
