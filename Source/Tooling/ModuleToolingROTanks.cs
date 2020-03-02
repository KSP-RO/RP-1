using RealFuels.Tanks;
using UnityEngine;

namespace RP0
{
    public class ModuleToolingROTanks : ModuleToolingPTank
    {
        protected override void LoadPartModules()
        {
            procTank = part.Modules["ModuleROTank"];
            rfTank = part.Modules.GetModule<ModuleFuelTanks>();
        }

        public override void GetDimensions(out float diam, out float len)
        {
            diam = 0f;
            len = 0f;

            if (procTank == null)
            {
                Debug.LogError("[ModuleTooling]: Could not find ModuleROTank to bind to");
                return;
            }

            if (length == null)
            {
                length = procTank.Fields["totalTankLength"];
                diam1 = procTank.Fields["largestDiameter"];
                diam2 = null;
            }

            if (diam1 == null || length == null)
            {
                Debug.LogError("[ModuleTooling] Could not bind to ROTank fields");
                return;
            }

            diam = diam1.GetValue<float>(procTank);
            len = length.GetValue<float>(procTank);
        }
    }
}
