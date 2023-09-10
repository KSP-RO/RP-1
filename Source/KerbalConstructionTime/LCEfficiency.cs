using System;
using System.Collections.Generic;
using RP0.DataTypes;
using UnityEngine;

namespace KerbalConstructionTime
{
    public class LCEfficiency : ConfigNodePersistenceBase, IConfigNode
    {
        [Persistent]
        protected double _efficiency = 0.5d;
        public double Efficiency => _efficiency;

        [Persistent]
        protected LCData _lcStats = new LCData();

        [Persistent]
        protected PersistentListValueType<Guid> _lcIDs = new PersistentListValueType<Guid>();

        public KCTObservableList<LCItem> _lcs = new KCTObservableList<LCItem>();

        protected Dictionary<LCEfficiency, double> _closenessCache = new Dictionary<LCEfficiency, double>();


        // Used by Persistence
        public LCEfficiency()
        {
            _lcs.Added += added;
            _lcs.Removed += removed;
        }

        // Used when created during runtime
        public LCEfficiency(LCItem lc)
        {
            _lcStats.SetFrom(lc);
            _lcStats.Name = "EfficiencyData";

            LCEfficiency closest = FindClosest(lc, out double closeness);
            if (closest == null)
                _efficiency = _MinEfficiency;
            else
                _efficiency = closest.PostClosenessStartingEfficiency(closeness);

            _lcs.Added += added;
            _lcs.Removed += removed;

            _lcs.Add(lc);
        }

        protected void Relink()
        {
            _ignoreObserve = true;
            _lcs.Clear();
            for (int i = _lcIDs.Count; i-- > 0;)
            {
                var id = _lcIDs[i];
                if (id == Guid.Empty)
                {
                    Debug.LogError("[RP-0] Error: found empty guid relinking LCEfficiency");
                    _lcIDs.RemoveAt(i);
                    continue;
                }

                var lc = KCTGameStates.FindLCFromID(id);
                if (lc == null)
                {
                    _lcIDs.RemoveAt(i);
                    Debug.LogError($"[RP-0] Error: could not find LC for guid {id} relinking LCEfficiency");
                    continue;
                }

                _lcs.Add(lc);
            }
            _ignoreObserve = false;
        }

        protected void RefreshCache()
        {
            _closenessCache.Clear();
            foreach (LCEfficiency e in KerbalConstructionTimeData.Instance.LCEfficiencies)
                if (e != this)
                    _closenessCache[e] = Formula.GetLCCloseness(_lcStats, e._lcStats);
        }

        public void RemoveLC(LCItem lc)
        {
            int idx = _lcs.IndexOf(lc);
            if (idx >= 0)
            {
                _lcs.RemoveAt(idx);
                if (_lcs.Count == 0)
                    ClearEmpty();
            }
            else
            {
                // Just in case we get in a bad state
                if (KerbalConstructionTimeData.Instance.LCToEfficiency.ContainsKey(lc))
                {
                    KerbalConstructionTimeData.Instance.LCToEfficiency.Remove(lc);
                    ClearEmpty();
                }
            }
        }

        private void ReceiveDistributedEfficiency(LCEfficiency e, double increase)
        {
            _closenessCache.TryGetValue(e, out double closeness);
            if (closeness == 0d)
                return;

            //bool noActive = true;
            //for (int i = _lcs.Count; i-- > 0;)
            //{
            //    if (_lcs[i].IsOperational)
            //    {
            //        noActive = false;
            //        break;
            //    }
            //}

            //if (noActive)
            //    return;

            IncreaseEfficiency(increase * closeness, false);
        }

        public void IncreaseEfficiency(double increase, bool distribute)
        {
            _efficiency += increase;
            if (_efficiency > _MaxEfficiency)
                _efficiency = _MaxEfficiency;

            if (distribute)
            {
                foreach (var e in KerbalConstructionTimeData.Instance.LCEfficiencies)
                    if (e != this)
                        e.ReceiveDistributedEfficiency(this, increase);
            }
        }

        public void IncreaseEfficiency(double timestep, double portionEngineers)
        {
            if (_efficiency == _MaxEfficiency)
                return;

            double eval = PresetManager.Instance.ActivePreset.GeneralSettings.EngineerSkillupRate.Evaluate((float)((_efficiency - _MinEfficiency) / _EfficiencyRange));
            double delta = _EfficiencyGainMult * eval * timestep * portionEngineers * (1d / (365.25d * 86400d));
            IncreaseEfficiency(delta, true);
        }


        private const int defaultSteps = 100;
        private const double minTimeStep = 86400d;

        /// <summary>
        /// Note: This will underestimate efficiency because it does not take shared efficiency gain into account.
        /// </summary>
        /// <param name="tdelta"></param>
        /// <param name="portionEngineers"></param>
        /// <param name="newEff"></param>
        /// <param name="startingEfficiency"></param>
        /// <returns></returns>
        public double PredictWeightedEfficiency(double tdelta, double portionEngineers, out double newEff, double startingEfficiency = -1d)
        {
            if (startingEfficiency < 0d)
                startingEfficiency = _efficiency;

            newEff = startingEfficiency;

            if (tdelta < 86400d || startingEfficiency >= _MaxEfficiency)
                return tdelta;

            int steps = defaultSteps;
            double timestep = tdelta / defaultSteps;
            double weightedEff = 0d;
            double remainingTime = tdelta;
            if (timestep < minTimeStep)
            {
                steps = (int)Math.Ceiling(tdelta / minTimeStep);
                timestep = tdelta / steps;
            }
            for (int i = steps; i-- > 0;)
            {
                remainingTime -= timestep;
                weightedEff += newEff * timestep;
                double eval = PresetManager.Instance.ActivePreset.GeneralSettings.EngineerSkillupRate.Evaluate((float)((newEff - _MinEfficiency) / _EfficiencyRange));
                newEff += _EfficiencyGainMult * eval * timestep * portionEngineers * (1d / (365.25d * 86400d));
                if (newEff >= _MaxEfficiency)
                {
                    newEff = _MaxEfficiency;
                    break;
                }
            }
            if (remainingTime > 0d)
                weightedEff += newEff * remainingTime;

            weightedEff /= tdelta;
            return weightedEff;
        }

        /// <summary>
        /// Note: This will underestimate efficiency because it does not take shared efficiency gain into account.
        /// </summary>
        /// <param name="tdelta"></param>
        /// <param name="portionEngineers"></param>
        /// <param name="startingEfficiency"></param>
        /// <returns></returns>
        public double PredictEfficiency(double tdelta, double portionEngineers, double startingEfficiency = -1d)
        {
            if (startingEfficiency < 0d)
                startingEfficiency = _efficiency;

            if (tdelta < 86400d || startingEfficiency >= _MaxEfficiency)
                return startingEfficiency;

            double newEff = startingEfficiency;
            int steps = defaultSteps;
            double timestep = tdelta / defaultSteps;
            if (timestep < minTimeStep)
            {
                steps = (int)Math.Ceiling(tdelta / minTimeStep);
                timestep = tdelta / steps;
            }
            for (int i = steps; i-- > 0;)
            {
                double eval = PresetManager.Instance.ActivePreset.GeneralSettings.EngineerSkillupRate.Evaluate((float)((newEff - _MinEfficiency) / _EfficiencyRange));
                newEff += _EfficiencyGainMult * eval * timestep * portionEngineers * (1d / (365.25d * 86400d));
                if (newEff >= _MaxEfficiency)
                {
                    return _MaxEfficiency;
                }
            }

            return newEff;
        }

        public bool Contains(Guid id) => _lcIDs.Contains(id);

        public bool Contains(LCItem lc) => _lcs.Contains(lc);

        public string FirstLCName() => _lcs.Count > 0 ? _lcs[0].Name : "No Named LC";

        static LCData _comparisonLCData = new LCData();

        public static LCEfficiency FindClosest(LCItem lc, out double bestCloseness)
        {
            _comparisonLCData.SetFrom(lc);
            return FindClosest(_comparisonLCData, out bestCloseness);
        }

        public static LCEfficiency FindClosest(LCData data, out double bestCloseness)
        {
            bestCloseness = 0d;
            LCEfficiency bestItem = null;
            
            foreach (var e in KerbalConstructionTimeData.Instance.LCEfficiencies)
            {
                double closeness = Formula.GetLCCloseness(data, e._lcStats);
                if (closeness > bestCloseness)
                {
                    bestCloseness = closeness;
                    bestItem = e;
                }
            }
            return bestItem;
        }

        public static LCEfficiency GetOrCreateEfficiencyForLC(LCItem lc, bool allowLookup)
        {
            LCEfficiency e;
            if (allowLookup && KerbalConstructionTimeData.Instance.LCToEfficiency.TryGetValue(lc, out e))
                return e;

            e = FindClosest(lc, out double closeness);
            if (closeness == 1d)
            {
                // If we modified the LC but didn't need a new LCEfficiency,
                // then we'll already be in this LCE's list. So we have to check.
                if (!e._lcs.Contains(lc))
                    e._lcs.Add(lc);

                return e;
            }

            e = new LCEfficiency(lc); // this will put it in the dict
            KerbalConstructionTimeData.Instance.LCEfficiencies.Add(e);
            RefreshAllCaches();
            return e;
        }

        public static void RelinkAll()
        {
            foreach (var e in KerbalConstructionTimeData.Instance.LCEfficiencies)
                e.Relink();

            if (!ClearEmpty())
                RefreshAllCaches();
        }

        protected static void RefreshAllCaches()
        {
            foreach (var e in KerbalConstructionTimeData.Instance.LCEfficiencies)
                e.RefreshCache();
        }

        public static bool ClearEmpty()
        {
            int oldCount = KerbalConstructionTimeData.Instance.LCEfficiencies.Count;
            for (int i = KerbalConstructionTimeData.Instance.LCEfficiencies.Count; i-- > 0;)
                if (KerbalConstructionTimeData.Instance.LCEfficiencies[i]._lcs.Count == 0)
                    KerbalConstructionTimeData.Instance.LCEfficiencies.RemoveAt(i);

            if (oldCount != KerbalConstructionTimeData.Instance.LCEfficiencies.Count)
            {
                RefreshAllCaches();
                return true;
            }

            return false;
        }

        private static double _MinEfficiency = 0d;
        public static double MinEfficiency => _MinEfficiency;

        private static double _MaxEfficiency = 1d;
        public static double MaxEfficiency => _MaxEfficiency;

        private static double _EfficiencyRange = 1d;

        private static double _EfficiencyGainMult = 1d;

        public static void RecalculateConstants()
        {
            _EfficiencyGainMult = RP0.CurrencyUtils.Rate(RP0.TransactionReasonsRP0.EfficiencyGainLC);
            double efficMult = RP0.CurrencyUtils.Rate(RP0.TransactionReasonsRP0.MaxEfficiencyLC);
            _MinEfficiency = efficMult * PresetManager.Instance.ActivePreset.GeneralSettings.LCEfficiencyMin;
            _MaxEfficiency = efficMult * PresetManager.Instance.ActivePreset.GeneralSettings.LCEfficiencyMax;
            _EfficiencyRange = _MaxEfficiency - _MinEfficiency;

            if (KSP.UI.Screens.MessageSystem.Instance != null)
            {
                var sb = StringBuilderCache.Acquire();
                foreach (var kvp in KerbalConstructionTimeData.Instance.LCToEfficiency)
                {
                    if (kvp.Value._efficiency < _MinEfficiency)
                    {
                        sb.Append("\n").Append(kvp.Key.Name);
                    }
                }
                string lcsModified = sb.ToStringAndRelease();
                if (!string.IsNullOrEmpty(lcsModified))
                    KSP.UI.Screens.MessageSystem.Instance.AddMessage(new KSP.UI.Screens.MessageSystem.Message("LC Efficiency Increases",
                        $"Due to improved technology the minimum efficiency for launch complexes is now {_MinEfficiency:P1} and the following complexes had efficiency raised to that level:" + lcsModified,
                        KSP.UI.Screens.MessageSystemButton.MessageButtonColor.GREEN, KSP.UI.Screens.MessageSystemButton.ButtonIcons.ACHIEVE));
            }
            foreach (var e in KerbalConstructionTimeData.Instance.LCEfficiencies)
                if (e._efficiency < _MinEfficiency)
                    e._efficiency = _MinEfficiency;
        }

        public double PostClosenessStartingEfficiency(double closeness)
        {
            return _MinEfficiency + (_efficiency - _MinEfficiency) * closeness;
        }

        // List operations ---------------------------------------------------
        /// <summary>
        /// Used during relinking to signal that certain collections should not be modified.
        /// </summary>
        private bool _ignoreObserve = false;

        private void added(int idx, LCItem lc)
        {
            if (KerbalConstructionTimeData.Instance.LCToEfficiency.TryGetValue(lc, out var oldEffic))
            {
                oldEffic._lcs.Remove(lc);
                if (oldEffic._lcs.Count == 0 && !_ignoreObserve)
                    ClearEmpty();
            }

            KerbalConstructionTimeData.Instance.LCToEfficiency[lc] = this;

            if (_ignoreObserve)
                return;

            _lcIDs.Insert(idx, lc.ID);
        }

        private void removed(int idx, LCItem lc)
        {
            KerbalConstructionTimeData.Instance.LCToEfficiency.Remove(lc);

            if (_ignoreObserve)
                return;

            _lcIDs.RemoveAt(idx);
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
