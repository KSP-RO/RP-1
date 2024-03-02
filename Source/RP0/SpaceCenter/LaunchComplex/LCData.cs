using System;
using System.Collections.Generic;
using UnityEngine;
using ROUtils.DataTypes;
using ROUtils;

namespace RP0
{
    public enum LaunchComplexType
    {
        Hangar,
        Pad,
    }

    public class LCData : IConfigNode
    {
        [Persistent] public string Name;
        /// <summary>
        /// Max vessel mass that the LC can support.
        /// </summary>
        [Persistent] public float massMax;
        /// <summary>
        /// The mass that LC was originally built with.
        /// </summary>
        [Persistent] public float massOrig;
        [Persistent] public Vector3 sizeMax;
        [Persistent] public LaunchComplexType lcType = LaunchComplexType.Pad;
        [Persistent] public bool isHumanRated;
        [Persistent] public PersistentDictionaryValueTypes<string, double> resourcesHandled = new PersistentDictionaryValueTypes<string, double>();

        /// <summary>
        /// Max allowable mass that the LC can be upgraded to.
        /// </summary>
        public float MaxPossibleMass => Math.Max(3f, Mathf.Floor(massOrig * 2f));
        /// <summary>
        /// Min allowable mass that the LC can be downgraded to.
        /// </summary>
        public float MinPossibleMass => Math.Max(1, Mathf.Ceil(massOrig * 0.5f));
        public bool IsMassWithinUpgradeMargin => massMax <= MaxPossibleMass;
        public bool IsMassWithinDowngradeMargin => massMax >= MinPossibleMass;
        public bool IsMassWithinUpAndDowngradeMargins => IsMassWithinUpgradeMargin && IsMassWithinDowngradeMargin;
        public static float CalcMassMin(float massMax) => massMax == float.MaxValue ? 0f : Mathf.Floor(massMax * 0.75f);
        public float MassMin => CalcMassMin(massMax);
        public static float CalcMassMaxFromMin(float massMin) => Mathf.Ceil(massMin / 0.75f);

        public static readonly LCData StartingHangar = new LCData("Hangar", float.MaxValue, float.MaxValue, new Vector3(40f, 10f, 40f), LaunchComplexType.Hangar, true, new PersistentDictionaryValueTypes<string, double>());

        public LCData() { }

        public LCData(string Name, float massMax, float massOrig, Vector3 sizeMax, LaunchComplexType lcType, bool isHumanRated, PersistentDictionaryValueTypes<string, double> resourcesHandled)
        {
            this.Name = Name;
            this.massMax = massMax;
            this.massOrig = massOrig;
            this.sizeMax = sizeMax;
            this.lcType = lcType;
            this.isHumanRated = isHumanRated;
            foreach (var kvp in resourcesHandled)
                this.resourcesHandled[kvp.Key] = kvp.Value;
            //TODO: If setting starting hangar, apply default resources, which are?
        }

        public LCData(LCData old)
        {
            SetFrom(old);
        }

        public LCData(LaunchComplex lc)
        {
            SetFrom(lc);
        }

        public void SetFrom(LCData old)
        {
            Name = old.Name;
            massMax = old.massMax;
            massOrig = old.massOrig;
            sizeMax = old.sizeMax;
            lcType = old.lcType;
            isHumanRated = old.isHumanRated;

            resourcesHandled.Clear();
            foreach (var kvp in old.resourcesHandled)
                resourcesHandled[kvp.Key] = kvp.Value;
        }

        public void SetFrom(LaunchComplex lc)
        {
            SetFrom(lc.Stats);
        }

        // NOTE: Not comparing name, which I think is correct here.
        public bool Compare(LaunchComplex lc) => massMax == lc.MassMax && sizeMax == lc.SizeMax && lcType == lc.LCType && isHumanRated == lc.IsHumanRated && PersistentDictionaryValueTypes<string, double>.AreEqual(resourcesHandled, lc.ResourcesHandled);
        public bool Compare(LCData data) => massMax == data.massMax && sizeMax == data.sizeMax && lcType == data.lcType && isHumanRated == data.isHumanRated && PersistentDictionaryValueTypes<string, double>.AreEqual(resourcesHandled, data.resourcesHandled);

        public float GetPadFracLevel()
        {
            float fractionalPadLvl = 0f;

            if (KCTUtilities.PadTons != null)
            {
                float unlimitedTonnageThreshold = 2500f;

                if (massMax >= unlimitedTonnageThreshold)
                {
                    int padLvl = KCTUtilities.PadTons.Length - 1;
                    fractionalPadLvl = padLvl;
                }
                else
                {
                    for (int i = 1; i < KCTUtilities.PadTons.Length; i++)
                    {
                        if (massMax < KCTUtilities.PadTons[i])
                        {
                            float lowerBound = KCTUtilities.PadTons[i - 1];
                            float upperBound = Math.Min(KCTUtilities.PadTons[i], unlimitedTonnageThreshold);
                            float fractionOverFullLvl = (massMax - lowerBound) / (upperBound - lowerBound);
                            fractionalPadLvl = (i - 1) + fractionOverFullLvl;

                            break;
                        }
                    }
                }
            }

            return fractionalPadLvl;
        }

        public double GetCostStats(out double costPad, out double costVAB, out double costResources)
        {
            Vector3 padSize = sizeMax; // we tweak it later.

            LCResourceType ignoredResType;
            if (lcType == LaunchComplexType.Pad)
            {
                ignoredResType = LCResourceType.PadIgnore;

                double mass = massMax;
                costPad = Math.Max(0d, Math.Pow(mass, 0.65d) * 2000d + Math.Pow(Math.Max(mass - 350, 0), 1.5d) * 2d - 2500d) + 500d;
            }
            else
            {
                ignoredResType = LCResourceType.HangarIgnore;

                costPad = 0f;
                padSize.y *= 5f;
            }
            costVAB = Math.Max(1000d, padSize.sqrMagnitude * 25d);
            if (isHumanRated)
            {
                costPad *= 1.5d;
                costVAB *= 2d;
            }

            costPad *= 0.5d;
            costVAB *= 0.5d;

            costResources = 0d;
            foreach (var kvp in resourcesHandled)
            {
                if((Database.ResourceInfo.LCResourceTypes.ValueOrDefault(kvp.Key) & ignoredResType) != 0)
                    continue;

                costResources += Formula.ResourceTankCost(kvp.Key, kvp.Value, false, lcType);
            }

            return costVAB + costPad + costResources;
        }

        private static HashSet<string> _resourceNames = new HashSet<string>();

        public double ResModifyCost(LCData old)
        {
            double totalCost = 0d;

            foreach (var res in old.resourcesHandled.Keys)
                _resourceNames.Add(res);
            foreach (var res in resourcesHandled.Keys)
                _resourceNames.Add(res);

            const double _DownsizeMult = -0.1d;
            foreach (var res in _resourceNames)
            {
                old.resourcesHandled.TryGetValue(res, out double oldAmount);
                resourcesHandled.TryGetValue(res, out double newAmount);

                double delta = newAmount - oldAmount;
                if (delta < 0d)
                    delta *= _DownsizeMult;

                totalCost += Formula.ResourceTankCost(res, delta, true, lcType);
            }

            return totalCost;
        }

        public void Load(ConfigNode node)
        {
            ConfigNode.LoadObjectFromConfig(this, node);
        }

        public void Save(ConfigNode node)
        {
            ConfigNode.CreateConfigFromObject(this, node);
        }
    }
}

/*
    KerbalConstructionTime (c) by Michael Marvin, Zachary Eck

    KerbalConstructionTime is licensed under a
    Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.

    You should have received a copy of the license along with this
    work. If not, see <http://creativecommons.org/licenses/by-nc-sa/4.0/>.
*/
