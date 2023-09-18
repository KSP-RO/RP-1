using System;
using UnityEngine;

namespace RP0
{
    public class ModuleToolingPFSide : ModuleToolingDiamLen
    {
        [KSPField]
        public string toolingTypeHinged = null;

        [KSPField]
        public float untooledMultiplierHinged = 0f;

        [KSPField]
        public float finalToolingCostMultiplierHinged = 0f;

        [KSPField]
        public string toolingTypeNonDecoupled = null;

        [KSPField]
        public float untooledMultiplierNonDecoupled = 0f;

        [KSPField]
        public float finalToolingCostMultiplierNonDecoupled = 0f;

        protected PartModule pmFairing;
        protected PartModule pmDecoupler;

        protected BaseField baseRad, maxRad, cylEnd, sideThickness, inlineHeight, noseHeightRatio, hingeEnabled, fairingStaged;

        protected bool EnsureFields()
        {
            if (baseRad == null)
            {
                baseRad = pmFairing.Fields["baseRad"];
                maxRad = pmFairing.Fields["maxRad"];
                cylEnd = pmFairing.Fields["cylEnd"];
                sideThickness = pmFairing.Fields["sideThickness"];
                inlineHeight = pmFairing.Fields["inlineHeight"];
                noseHeightRatio = pmFairing.Fields["noseHeightRatio"];
                hingeEnabled = pmFairing.Fields["hingeEnabled"];
                fairingStaged = pmDecoupler.Fields["fairingStaged"];

                if (baseRad == null)
                {
                    RP0Debug.LogError($"[ModuleTooling] Could not bind to ProceduralFairingSide fields on {part}");
                    return false;
                }
            }

            return true;
        }

        protected override void LoadPartModules()
        {
            base.LoadPartModules();
            pmFairing = part.Modules["ProceduralFairingSide"];
            pmDecoupler = part.Modules["ProceduralFairingDecoupler"];
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (!EnsureFields())
                return;

            if (state == StartState.Editor)
            {
                fairingStaged.uiControlEditor.onFieldChanged += OnStateUpdated;
                hingeEnabled.uiControlEditor.onFieldChanged += OnStateUpdated;
                UpdateToolingAndCosts();
            }
        }

        public override void GetDimensions(out float diam, out float len)
        {
            diam = 0f;
            len = 0f;

            if (pmFairing == null)
            {
                RP0Debug.LogError($"[ModuleTooling] Could not bind to ProceduralFairingSide module on {part}");
                return;
            }
            if (!EnsureFields())
                return;

            float baseRadF, maxRadF, cylEndF, sideThicknessF, inlineHeightF, noseHeightRatioF;
            baseRadF = baseRad.GetValue<float>(pmFairing);
            maxRadF = maxRad.GetValue<float>(pmFairing);
            cylEndF = cylEnd.GetValue<float>(pmFairing);
            sideThicknessF = sideThickness.GetValue<float>(pmFairing);
            inlineHeightF = inlineHeight.GetValue<float>(pmFairing);
            noseHeightRatioF = noseHeightRatio.GetValue<float>(pmFairing);

            diam = (Math.Max(baseRadF, maxRadF) + sideThicknessF) * 2f;
            len = (inlineHeightF > 0) ? inlineHeightF : (noseHeightRatioF * diam / 2) + cylEndF;
        }

        public void UpdateToolingAndCosts()
        {
            if (toolingTypeNonDecoupled == null || part?.partInfo?.partPrefab == null || untooledMultiplierNonDecoupled == 0f || finalToolingCostMultiplierNonDecoupled == 0f)
                return;

            if (!EnsureFields())
                return;

            bool isDecoupled = fairingStaged.GetValue<bool>(pmDecoupler);
            bool isHinged = hingeEnabled.GetValue<bool>(pmFairing);

            var toolingPrefabModule = part.partInfo.partPrefab.FindModuleImplementing<ModuleToolingPFSide>();
            if (isDecoupled)
            {
                toolingType = toolingPrefabModule.toolingType;
                untooledMultiplier = toolingPrefabModule.untooledMultiplier;
                finalToolingCostMultiplier = toolingPrefabModule.finalToolingCostMultiplier;
            }
            else if (isHinged)
            {
                toolingType = toolingTypeHinged;
                untooledMultiplier = untooledMultiplierHinged;
                finalToolingCostMultiplier = finalToolingCostMultiplierHinged;
            }
            else
            {
                toolingType = toolingTypeNonDecoupled;
                untooledMultiplier = untooledMultiplierNonDecoupled;
                finalToolingCostMultiplier = finalToolingCostMultiplierNonDecoupled;
            }
        }

        private void OnStateUpdated(BaseField bf, object obj)
        {
            UpdateToolingAndCosts();

            foreach (Part p in part.symmetryCounterparts)
            {
                p.Modules.GetModule<ModuleToolingPFSide>().UpdateToolingAndCosts();
            }
        }
    }
}
