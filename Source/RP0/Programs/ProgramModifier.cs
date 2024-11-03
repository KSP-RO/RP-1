using KSP.Localization;
using System.Collections.Generic;
using static ConfigNode;
using static RP0.Programs.Program;

namespace RP0.Programs
{
    public class ProgramModifier : IConfigNode
    {
        [Persistent]
        public string srcProgram;
        [Persistent]
        public string tgtProgram;
        [Persistent]
        public double nominalDurationYears = -1;
        [Persistent]
        public double baseFunding = -1;
        [Persistent]
        public string fundingCurve = null;
        [Persistent]
        public double repDeltaOnCompletePerYearEarly = -1;
        [Persistent]
        public double repPenaltyPerYearLate = -1;
        [Persistent]
        public float repToConfidence = -1f;
        [Persistent]
        public int slots = -1;
        public Dictionary<Speed, float> confidenceCosts = new Dictionary<Speed, float>();

        public ProgramModifier()
        {
        }

        public ProgramModifier(ConfigNode n) : this()
        {
            Load(n);
        }

        public void Load(ConfigNode node)
        {
            LoadObjectFromConfig(this, node);
            LoadConfidenceCosts(node, confidenceCosts);
        }

        public void Save(ConfigNode node)
        {
            CreateConfigFromObject(this, node);
        }

        public void Apply(Program program)
        {
            if (nominalDurationYears != -1)
                program.nominalDurationYears = nominalDurationYears;

            if (baseFunding != -1)
                program.baseFunding = baseFunding;

            if (fundingCurve != null)
                program.fundingCurve = fundingCurve;

            if (repDeltaOnCompletePerYearEarly != -1)
                program.repDeltaOnCompletePerYearEarly = repDeltaOnCompletePerYearEarly;

            if (repPenaltyPerYearLate != -1)
                program.repPenaltyPerYearLate = repPenaltyPerYearLate;

            if (repToConfidence != -1)
                program.repToConfidence = repToConfidence;

            if (slots != -1)
                program.slots = slots;

            if (confidenceCosts.Count > 0)
            {
                foreach (KeyValuePair<Speed, float> kvp in confidenceCosts)
                {
                    program.confidenceCosts[kvp.Key] = kvp.Value;
                }
            }
        }

        public override string ToString() => $"{srcProgram} -> {tgtProgram}";

        public string GetDiffString()
        {
            Program baseline = ProgramHandler.ProgramDict[tgtProgram].GetStrategy().Program;

            var sb = StringBuilderCache.Acquire();
            sb.AppendFormat("* {0}\n", baseline.title);

            const string diffFormat = "    - {0}: {1} -> {2}\n";
            if (nominalDurationYears != -1 && baseline.nominalDurationYears != nominalDurationYears)
                sb.AppendFormat(diffFormat, "Nominal duration (years)", baseline.nominalDurationYears, nominalDurationYears);

            if (baseFunding != -1 && baseline.baseFunding != baseFunding)
                sb.AppendFormat(diffFormat, "Total funds", CalcTotalFunding(baseline.baseFunding).ToString("N0"), CalcTotalFunding(baseFunding).ToString("N0"));

            if (fundingCurve != null && baseline.fundingCurve != fundingCurve)
                sb.AppendFormat(diffFormat, "Funding curve", baseline.fundingCurve, fundingCurve);

            if (slots != -1 && baseline.slots != slots)
                sb.AppendFormat(diffFormat, "Slots", baseline.slots, slots);

            if (confidenceCosts.Count > 0)
            {
                foreach (KeyValuePair<Speed, float> kvp in confidenceCosts)
                {
                    if (baseline.confidenceCosts[kvp.Key] != kvp.Value)
                    {
                        string speedTitle = Localizer.GetStringByTag("#rp0_Admin_Program_Speed" + (int)kvp.Key);
                        sb.AppendFormat("    - Confidence for {0}: {1} -> {2}\n", speedTitle, baseline.confidenceCosts[kvp.Key], kvp.Value);
                    }
                }
            }

            return sb.ToStringAndRelease();
        }
    }
}
