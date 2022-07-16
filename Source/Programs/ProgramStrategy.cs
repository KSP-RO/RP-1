using Strategies;
using UnityEngine;
using UniLinq;

namespace RP0.Programs
{
    public class ProgramStrategy : Strategy
    {
        public bool NextTextIsShowSelected = false;

        protected override string GetEffectText()
        {
            if (ProgramHandler.Instance == null)
                return base.GetEffectText();

            Program program = ProgramHandler.Instance.ActivePrograms.Find(p => p.name == Config.Name);
            if (program == null)
                program = ProgramHandler.Instance.CompletedPrograms.Find(p => p.name == Config.Name);

            bool wasAccepted = program != null;

            if (program == null)
                program = ProgramHandler.Programs.Find(p => p.name == Config.Name);

            if (program == null)
                return base.GetEffectText();

            string objectives = string.Empty, requirements = string.Empty;
            if (NextTextIsShowSelected)
            {
                var tmp = program.ObjectivesBlock?.ToString(doColoring: wasAccepted);
                objectives = $"<b>Objectives</b>:\n{(string.IsNullOrWhiteSpace(tmp) ? "None" : tmp)}";

                tmp = program.RequirementsBlock?.ToString(doColoring: !wasAccepted);
                requirements = $"<b>Requirements</b>:\n{(string.IsNullOrWhiteSpace(tmp) ? "None" : tmp)}";
            }
            else
            {
                objectives = $"<b>Objectives</b>: {program.objectivesPrettyText}";
                requirements = $"<b>Requirements</b>: {program.requirementsPrettyText}";
            }

            string text = $"{objectives}\n\nTotal Funds: <sprite=\"CurrencySpriteAsset\" name=\"Funds\" tint=1>{program.TotalFunding:N0}\n";
            if (wasAccepted)
            {
                text += $"Funds Paid Out: <sprite=\"CurrencySpriteAsset\" name=\"Funds\" tint=1>{program.fundsPaidOut:N0}\nAccepted: {KSPUtil.dateTimeFormatter.PrintDateCompact(program.acceptedUT, false, false)}\n";
                if (program.IsComplete)
                    text += $"Completed: {KSPUtil.dateTimeFormatter.PrintDateCompact(program.completedUT, false, false)}";
                else
                    text += $"Deadline: {KSPUtil.dateTimeFormatter.PrintDateCompact(program.acceptedUT + program.nominalDurationYears * 365.25d * 86400d, false, false)}";
            }
            else
            {
                text = $"{requirements}\n\n{text}Nominal Duration: {program.nominalDurationYears:0.#} years";
            }

            if (NextTextIsShowSelected && !wasAccepted)
            {
                if (program.programsToDisableOnAccept.Count > 0)
                {
                    text += "\nWill disable the following on accept:";
                    foreach (var s in program.programsToDisableOnAccept)
                        text += $"\n{ProgramHandler.PrettyPrintProgramName(s)}";
                }

                text += "\n\nFunding Summary:";
                double paid = 0d;
                int max = (int)(program.nominalDurationYears) + 1;
                for (int i = 1; i < max; ++i)
                {
                    const double secPerYear = 365.25d * 86400d;
                    double fundAtYear = program.GetFundsAtTime(secPerYear * i);
                    double amtPaid = fundAtYear - paid;
                    paid = fundAtYear;
                    text += $"\nYear {(max > 10 ? " " : string.Empty)}{i}:  <sprite=\"CurrencySpriteAsset\" name=\"Funds\" tint=1>{amtPaid:N0}";
                }
            }

            NextTextIsShowSelected = false;

            return text;
        }

        protected override bool CanActivate(ref string reason)
        {
            if (ProgramHandler.Instance == null)
            {
                reason = "An error occurred during loading. The Program Handler is null!";
                return false;
            }

            if (ProgramHandler.Instance.CompletedPrograms.Any(p => p.name == Config.Name))
            {
                reason = "This Program has already been completed.";
                return false;
            }

            if (ProgramHandler.Instance.ActivePrograms.Any(p => p.name == Config.Name))
            {
                reason = "This Program is currently active.";
                return false;
            }

            if (ProgramHandler.Instance.DisabledPrograms.Contains(Config.Name))
            {
                reason = "This Program is disabled.";
                return false;
            }

            Program program = ProgramHandler.Programs.Find(p => p.name == Config.Name);
            if (program == null)
            {
                reason = "An error occurred during loading. This Program cannot be found!";
                return false;
            }

            if (!program.CanAccept)
            {
                reason = "This Program has unmet requirements.";
                return false;
            }

            // Handled by base in the Admin screen.
            //if (ProgramHandler.Instance.ActivePrograms.Count >= ProgramHandler.Instance.ActiveProgramLimit)
            //    return false;

            return true;
        }

        protected override bool CanDeactivate(ref string reason)
        {
            if (ProgramHandler.Instance == null)
            {
                reason = "An error occurred during loading. The Program Handler is null!";
                return false;
            }

            Program p = ProgramHandler.Instance.ActivePrograms.Find(p2 => p2.name == Config.Name);
            if (p == null)
            {
                reason = "This Program cannot be found in the active programs.";
                return false;
            }

            if (!p.CanComplete)
            {
                if (p.AllObjectivesMet && p.IsActive && !p.IsComplete)
                {
                    Debug.LogError($"[RP-0]: Program {p.name} was incorrectly not marked as complete. Marking complete now.");
                    p.MarkObjectivesComplete();
                }
                else
                {
                    reason = "This Program has unmet objectives.";
                    return false;
                }
            }

            return true;
        }

        protected override void OnRegister()
        {
            base.OnRegister();

            if (ProgramHandler.Instance && ProgramHandler.Instance.IsInAdmin)
                if (!ProgramHandler.Instance.ActivateProgram(Config.Name))
                    Debug.LogError($"[RP-0] Error: Can't find program {Config.Name} to accept!");
        }

        protected override void OnUnregister()
        {
            base.OnUnregister();

            if (ProgramHandler.Instance && ProgramHandler.Instance.IsInAdmin)
            {
                if (!ProgramHandler.Instance.CompleteProgram(Config.Name))
                    Debug.LogError($"[RP-0] Error: Can't find program {Config.Name} to complete!");
            }
        }
    }
}
