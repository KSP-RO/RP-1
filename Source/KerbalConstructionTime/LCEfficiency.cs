using System;
using System.Collections.Generic;
using RP0.DataTypes;

namespace KerbalConstructionTime
{
    public class LCEfficiency : IConfigNode
    {
        [Persistent]
        protected double _efficiency = 0.5d;
        public double Efficiency => _efficiency;

        [Persistent]
        protected LCItem.LCData _lcStats = new LCItem.LCData();

        [Persistent]
        protected PersistentListValueType<Guid> _lcIDs = new PersistentListValueType<Guid>();

        public KCTObservableList<LCItem> _lcs = new KCTObservableList<LCItem>();


        public LCEfficiency()
        {
            _lcs.Added += added;
            _lcs.Removed += removed;
        }

        public LCEfficiency(LCItem.LCData stats, double efficiency = 0)
        {
            _efficiency = Math.Max(_MinEfficiency, efficiency);
            _lcStats.SetFrom(stats);

            _lcs.Added += added;
            _lcs.Removed += removed;
        }

        public LCEfficiency(LCItem lc)
        {
            _lcStats.SetFrom(lc);
            

            LCEfficiency closest = FindClosest(lc, out double closeness);
            if (closest == null)
                _efficiency = _MinEfficiency;
            else
                _efficiency = closest.PostClosenessStartingEfficiency(closeness);

            _lcs.Added += added;
            _lcs.Removed += removed;

            _lcs.Add(lc);
        }

        public void Load(ConfigNode node)
        {
            ConfigNode.LoadObjectFromConfig(this, node);
        }

        public void Save(ConfigNode node)
        {
            ConfigNode.CreateConfigFromObject(this, node);
        }

        public void Relink()
        {
            _ignoreObserve = true;
            _lcs.Clear();
            foreach (var id in _lcIDs)
            {
                _lcs.Add(KCTGameStates.FindLCFromID(id));
            }
            _ignoreObserve = false;
        }

        public void RemoveLC(LCItem lc)
        {
            if (!_lcs.Contains(lc) && KerbalConstructionTimeData.Instance.LCToEfficiency.ContainsKey(lc))
                KerbalConstructionTimeData.Instance.LCToEfficiency.Remove(lc);

            _lcs.Remove(lc);
        }

        private void ReceiveDistributedEfficiency(LCEfficiency e, double increase)
        {
            double closeness = Utilities.GetLCCloseness(_lcStats, e._lcStats);
            if (closeness == 0d)
                return;

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

            double eval = PresetManager.Instance.ActivePreset.GeneralSettings.EngineerSkillupRate.Evaluate((float)((Efficiency - _MinEfficiency) / _EfficiencyRange));
            double delta = _EfficiencyGainMult * eval * timestep * portionEngineers / (365.25d * 86400d);
            IncreaseEfficiency(delta, true);
        }

        public bool Contains(Guid id) => _lcIDs.Contains(id);

        public bool Contains(LCItem lc) => _lcs.Contains(lc);

        public string FirstLCName() => _lcs.Count > 0 ? _lcs[0].Name : "No Named LC";

        static LCItem.LCData _comparisonLCData = new LCItem.LCData();

        public static LCEfficiency FindClosest(LCItem lc, out double bestCloseness)
        {
            _comparisonLCData.SetFrom(lc);
            return FindClosest(_comparisonLCData, out bestCloseness);
        }

        public static LCEfficiency FindClosest(LCItem.LCData data, out double bestCloseness)
        {
            bestCloseness = 0d;
            LCEfficiency bestItem = null;
            
            foreach (var e in KerbalConstructionTimeData.Instance.LCEfficiencies)
            {
                double closeness = Utilities.GetLCCloseness(data, e._lcStats);
                if (closeness > bestCloseness)
                {
                    bestCloseness = closeness;
                    bestItem = e;
                }
            }
            return bestItem;
        }

        public static LCEfficiency GetOrCreateEfficiencyForLC(LCItem lc)
        {
            LCEfficiency e;
            if (KerbalConstructionTimeData.Instance.LCToEfficiency.TryGetValue(lc, out e))
                return e;

            e = FindClosest(lc, out double closeness);
            if (closeness == 1d)
            {
                e._lcs.Add(lc);
                return e;
            }

            e = new LCEfficiency(lc); // this will put it in the dict
            KerbalConstructionTimeData.Instance.LCEfficiencies.Add(e);
            return e;
        }

        public static void RelinkAll()
        {
            foreach (var e in KerbalConstructionTimeData.Instance.LCEfficiencies)
                e.Relink();
        }

        public static void ClearEmpty()
        {
            for (int i = KerbalConstructionTimeData.Instance.LCEfficiencies.Count; i-- > 0;)
                if (KerbalConstructionTimeData.Instance.LCEfficiencies[i]._lcs.Count == 0)
                    KerbalConstructionTimeData.Instance.LCEfficiencies.RemoveAt(i);
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
                        "The following Launch Complexes have had their efficiency increased due to improved technology:" + lcsModified,
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
        private bool _ignoreObserve = false;

        private void added(int idx, LCItem lc)
        {
            if (KerbalConstructionTimeData.Instance.LCToEfficiency.TryGetValue(lc, out var oldEffic))
                oldEffic._lcs.Remove(lc);

            KerbalConstructionTimeData.Instance.LCToEfficiency[lc] = this;

            if (_ignoreObserve)
                return;

            _lcIDs.Insert(idx, lc.ID);
        }

        private void removed(int idx, LCItem lc)
        {
            if (KerbalConstructionTimeData.Instance.LCToEfficiency.ContainsKey(lc))
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
