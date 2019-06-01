using System;
using UnityEngine;

namespace RP0
{
    public class ModuleToolingPFSide : ModuleToolingDiamLen
    {
        [KSPField]
        public string toolingTypeNonDecoupled = null;

        [KSPField]
        public float untooledMultiplierNonDecoupled = 0f;

        [KSPField]
        public float finalToolingCostMultiplierNonDecoupled = 0f;

        [KSPField]
        public float costPerTonneNonDecoupled = 0f;

        protected PartModule pmFairing;
        protected PartModule pmDecoupler;

        protected BaseField baseRad, maxRad, cylEnd, sideThickness, inlineHeight, noseHeightRatio, costPerTonne;

        protected override void LoadPartModules()
        {
            base.LoadPartModules();
            pmFairing = part.Modules["ProceduralFairingSide"];
            pmDecoupler = part.Modules["ProceduralFairingDecoupler"];
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            if (state == StartState.Editor && pmDecoupler != null && pmFairing != null)
            {
                var bf = pmDecoupler.Fields["fairingStaged"];
                ((UI_Toggle)bf.uiControlEditor).onFieldChanged += OnFairingStagedChanged;

                if (part.partInfo != null)
                {
                    var isDecoupled = bf.GetValue<bool>(pmDecoupler);
                    UpdateToolingAndCosts(isDecoupled);
                }
            }
        }

        public override void GetDimensions(out float diam, out float len)
        {
            diam = 0f;
            len = 0f;

            if (pmFairing == null)
            {
                Debug.LogError("[ModuleTooling] Could not find PF module to bind to");
                return;
            }

            if (baseRad == null)
            {
                baseRad = pmFairing.Fields["baseRad"];
                maxRad = pmFairing.Fields["maxRad"];
                cylEnd = pmFairing.Fields["cylEnd"];
                sideThickness = pmFairing.Fields["sideThickness"];
                inlineHeight = pmFairing.Fields["inlineHeight"];
                noseHeightRatio = pmFairing.Fields["noseHeightRatio"];
                costPerTonne = pmFairing.Fields["costPerTonne"];

                if (baseRad == null)
                {
                    Debug.LogError("[ModuleTooling] Could not bind to fields in PF module");
                    return;
                }
            }
            float baseRadF, maxRadF, cylEndF, sideThicknessF, inlineHeightF, noseHeightRatioF;
            baseRadF = baseRad.GetValue<float>(pmFairing);
            maxRadF = maxRad.GetValue<float>(pmFairing);
            cylEndF = cylEnd.GetValue<float>(pmFairing);
            sideThicknessF = sideThickness.GetValue<float>(pmFairing);
            inlineHeightF = inlineHeight.GetValue<float>(pmFairing);
            noseHeightRatioF = noseHeightRatio.GetValue<float>(pmFairing);

            diam = (Math.Max(baseRadF, maxRadF) + sideThicknessF) * 2f;
            if (inlineHeightF > 0f)
                len = inlineHeightF;
            else
                len = noseHeightRatioF * diam * 0.5f + cylEndF;
        }

        public void UpdateToolingAndCosts(bool isDecoupled)
        {
            if (toolingTypeNonDecoupled == null || costPerTonneNonDecoupled == 0f || untooledMultiplierNonDecoupled == 0f || finalToolingCostMultiplierNonDecoupled == 0f)
                return;

            var toolingPrefabModule = part.partInfo.partPrefab.FindModuleImplementing<ModuleToolingPFSide>();
            var fairingPrefabModule = part.partInfo.partPrefab.Modules["ProceduralFairingSide"];
            var bf = fairingPrefabModule.Fields["costPerTonne"];
            var prefabCostPerTonne = bf.GetValue<float>(fairingPrefabModule);

            if (costPerTonne == null)
            {
                costPerTonne = pmFairing.Fields["costPerTonne"];
            }

            toolingType = isDecoupled ? toolingPrefabModule.toolingType : toolingTypeNonDecoupled;
            untooledMultiplier = isDecoupled ? toolingPrefabModule.untooledMultiplier : untooledMultiplierNonDecoupled;
            finalToolingCostMultiplier = isDecoupled ? toolingPrefabModule.finalToolingCostMultiplier : finalToolingCostMultiplierNonDecoupled;
            costPerTonne.SetValue(isDecoupled ? prefabCostPerTonne : costPerTonneNonDecoupled, pmFairing);
        }

        private void OnFairingStagedChanged(BaseField bf, object obj)
        {
            var isDecoupled = bf.GetValue<bool>(pmDecoupler);
            UpdateToolingAndCosts(isDecoupled);

            for (int i = 0; i < part.symmetryCounterparts.Count; i++)
            {
                var p = part.symmetryCounterparts[i];
                var pm = p.Modules.GetModule<ModuleToolingPFSide>();
                pm.UpdateToolingAndCosts(isDecoupled);
            }
        }
    }
}
