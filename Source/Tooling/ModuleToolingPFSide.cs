using System;
using System.Linq;
using System.Reflection;
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

        // Prep to support PF 1.8.x or 2+: Will no longer reach into PartModule and fiddle with costPerTonne.
        private bool UpdateCostPerTonne => PFAssemblyVersion.CompareTo("6") < 0;
        private Assembly PFAssembly;
        private string PFAssemblyVersion => PFAssembly is Assembly ? System.Diagnostics.FileVersionInfo.GetVersionInfo(PFAssembly.Location).FileVersion : string.Empty;

        protected override void LoadPartModules()
        {
            base.LoadPartModules();
            pmFairing = part.Modules["ProceduralFairingSide"];
            pmDecoupler = part.Modules["ProceduralFairingDecoupler"];
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            PFAssembly = AssemblyLoader.loadedAssemblies.FirstOrDefault(a => string.Equals(a.assembly.GetName().Name, "ProceduralFairings", StringComparison.OrdinalIgnoreCase))?.assembly;
            if (state == StartState.Editor && pmDecoupler?.Fields["fairingStaged"] is BaseField bf && pmFairing is PartModule)
            {
                bf.uiControlEditor.onFieldChanged += OnFairingStagedChanged;
                UpdateToolingAndCosts(bf.GetValue<bool>(pmDecoupler));
            }
        }

        public override void GetDimensions(out float diam, out float len)
        {
            diam = 0f;
            len = 0f;

            if (pmFairing == null)
            {
                Debug.LogError($"[ModuleTooling] Could not bind to ProceduralFairingSide module on {part}");
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
                    Debug.LogError($"[ModuleTooling] Could not bind to ProceduralFairingSide fields on {part}");
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
            len = (inlineHeightF > 0) ? inlineHeightF : (noseHeightRatioF * diam / 2) + cylEndF;
        }

        public void UpdateToolingAndCosts(bool isDecoupled)
        {
            if (toolingTypeNonDecoupled == null || part?.partInfo?.partPrefab == null || costPerTonneNonDecoupled == 0f || untooledMultiplierNonDecoupled == 0f || finalToolingCostMultiplierNonDecoupled == 0f)
                return;

            var toolingPrefabModule = part.partInfo.partPrefab.FindModuleImplementing<ModuleToolingPFSide>();

            if (UpdateCostPerTonne && (costPerTonne ?? pmFairing.Fields["costPerTonne"]) is BaseField field)
            {
                var fairingPrefabModule = part.partInfo.partPrefab.Modules["ProceduralFairingSide"];
                var prefabCostPerTonne = fairingPrefabModule?.Fields["costPerTonne"]?.GetValue<float>(fairingPrefabModule) ?? 0;
                field.SetValue(isDecoupled ? prefabCostPerTonne : costPerTonneNonDecoupled, pmFairing);
            }

            toolingType = isDecoupled ? toolingPrefabModule.toolingType : toolingTypeNonDecoupled;
            untooledMultiplier = isDecoupled ? toolingPrefabModule.untooledMultiplier : untooledMultiplierNonDecoupled;
            finalToolingCostMultiplier = isDecoupled ? toolingPrefabModule.finalToolingCostMultiplier : finalToolingCostMultiplierNonDecoupled;
        }

        private void OnFairingStagedChanged(BaseField bf, object obj)
        {
            var isDecoupled = bf.GetValue<bool>(pmDecoupler);
            UpdateToolingAndCosts(isDecoupled);
            foreach (Part p in part.symmetryCounterparts)
            {
                p.Modules.GetModule<ModuleToolingPFSide>().UpdateToolingAndCosts(isDecoupled);
            }
        }
    }
}
