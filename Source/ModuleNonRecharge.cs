using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using KSP;

namespace RP0
{
    public class ModuleNonRecharge : PartModule
    {
        [KSPField]
        public string resourceName = "ElectricCharge";

        protected PartResource res;

        public override string GetInfo()
        {
            return "This module has non-rechargeable " + resourceName + ". Once depleted it cannot be replenished.";
        }

        public override void OnAwake()
        {
            base.OnAwake();

            res = part.Resources.Get(resourceName);
            if (res == null)
                Debug.LogError("ModuleNonRecharge ERROR: cannot find resource " + resourceName);
        }

        protected void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || vessel == null)
                return;

            if ((object)res != null)
            {
                double target = 0.0001d;
                if (res.amount > target)
                    target = res.amount;

                if (res.maxAmount > target)
                    res.maxAmount = target;
            }
        }
    }
}
