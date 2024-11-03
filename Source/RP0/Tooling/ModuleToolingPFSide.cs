﻿using System;
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

        protected BaseField diameterFld, heightFld, hingeEnabledFld, fairingStagedFld;

        protected bool EnsureFields()
        {
            if (fairingStagedFld == null)
            {
                diameterFld = pmFairing.Fields["diameter"];
                heightFld = pmFairing.Fields["height"];
                hingeEnabledFld = pmFairing.Fields["hingeEnabled"];
                fairingStagedFld = pmDecoupler.Fields["fairingStaged"];

                if (diameterFld == null)
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
                fairingStagedFld.uiControlEditor.onFieldChanged += OnStateUpdated;
                hingeEnabledFld.uiControlEditor.onFieldChanged += OnStateUpdated;
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

            if (diameterFld !=  null)
            {
                diam = diameterFld.GetValue<float>(pmFairing);
                len = heightFld.GetValue<float>(pmFairing);
            }
        }

        public void UpdateToolingAndCosts()
        {
            if (toolingTypeNonDecoupled == null || part?.partInfo?.partPrefab == null || untooledMultiplierNonDecoupled == 0f || finalToolingCostMultiplierNonDecoupled == 0f)
                return;

            if (!EnsureFields())
                return;

            bool isDecoupled = fairingStagedFld.GetValue<bool>(pmDecoupler);
            bool isHinged = hingeEnabledFld.GetValue<bool>(pmFairing);

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
