using PreFlightTests;
using System;
using KSP.Localization;

namespace RP0.DesignConcerns
{
    public class UntooledParts : DesignConcernBase
    {
        private int failureCount = 0;
        public override string GetConcernDescription()
        {
            if (failureCount < 2)
                return Localizer.GetStringByTag("#pr0ER_Concern_UntooledParts_DescriptionSingle");

            return Localizer.Format("#pr0ER_Concern_UntooledParts_DescriptionMany", failureCount.ToString());
        }

        public override string GetConcernTitle()
        {
            return Localizer.GetStringByTag("#pr0ER_Concern_UntooledParts_Title");
        }

        public override DesignConcernSeverity GetSeverity()
        {
            return DesignConcernSeverity.WARNING;
        }

        public override bool TestCondition()
        {
            failureCount = 0;

            if (EditorLogic.fetch.ship == null)
                return true;

            foreach (Part p in EditorLogic.fetch.ship.parts)
            {
                foreach (PartModule pm in p.Modules)
                {
                    if (pm is ModuleTooling tooling)
                    {
                        if (!tooling.IsUnlocked())
                            ++failureCount;
                    }
                }
            }

            return failureCount == 0;
        }
    }
}
