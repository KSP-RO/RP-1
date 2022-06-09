using Strategies;
using UnityEngine;
using UniLinq;

namespace RP0.Programs
{
    public class ProgramStrategy : Strategy
    {
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

            string text = $"Objectives: {program.objectivesPrettyText}\nTotal Funds: {program.TotalFunding:N0}\n";
            if (wasAccepted)
            {
                text += $"Funds Paid Out: {program.fundsPaidOut:N0}\nAccepted: {KSPUtil.dateTimeFormatter.PrintDateCompact(program.acceptedUT, false, false)}\n";
                if (program.IsComplete)
                    text += $"Completed: {KSPUtil.dateTimeFormatter.PrintDateCompact(program.completedUT, false, false)}";
                else
                    text += $"Deadline: {KSPUtil.dateTimeFormatter.PrintDateCompact(program.acceptedUT + program.nominalDurationYears * 365.25d * 86400d, false, false)}";
            }
            else
            {
                text = $"Requirements: {program.requirementsPrettyText}\n{text}Nominal Duration: {program.nominalDurationYears:0.#} years";
            }

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
                reason = "This Program has unmet objectives.";
                return false;
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
