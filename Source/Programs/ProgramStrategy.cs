using Strategies;
using UnityEngine;
using UniLinq;

namespace RP0.Programs
{
    public class ProgramStrategy : Strategy
    {
        public bool NextTextIsShowSelected = false;

        protected Program _program;
        public Program Program => _program;
        public void SetProgram(Program p) { _program = p; }
        public void SetSpeed(Program.Speed spd) { _program.speed = spd; }
        public Program.Speed ProgramSpeed => _program?.speed ?? Program.Speed.Slow;

        public void OnSetupConfig()
        {
            // StrategySystem's OnLoad (that creates Strategy objects)
            // waits a frame before firing, which happily means ProgramHandler is live.
            _program = ProgramHandler.Instance.ActivePrograms.Find(p => p.name == Config.Name);
            if (_program == null)
            {
                _program = ProgramHandler.Instance.CompletedPrograms.Find(p => p.name == Config.Name);
            }

            if (_program == null)
            {
                Program source = ProgramHandler.Programs.Find(p => p.name == Config.Name);
                if (source == null)
                {
                    Debug.LogError($"[RP-0] ProgramStrategy: Error finding program {Config.Name}");
                    return;
                }
                // Create a copy so we can mess with the speed
                _program = new Program(source);
            }
        }

        protected override string GetEffectText()
        {
            bool extendedInfo = NextTextIsShowSelected;
            NextTextIsShowSelected = false;

            if (_program == null)
                return "Error finding program!";

            return _program.GetDescription(extendedInfo);
        }

        protected override bool CanActivate(ref string reason)
        {
            if (ProgramHandler.Instance == null)
            {
                reason = "An error occurred during loading. The Program Handler is null!";
                return false;
            }

            if (_program == null)
            {
                reason = "An error occurred during loading. The program field is null!";
                return false;
            }

            if (_program.IsComplete)
            {
                reason = "This Program has already been completed.";
                return false;
            }

            if (_program.IsActive)
            {
                reason = "This Program is currently active.";
                return false;
            }

            if (ProgramHandler.Instance.DisabledPrograms.Contains(Config.Name))
            {
                reason = "This Program is disabled.";
                return false;
            }

            if (!_program.CanAccept)
            {
                reason = "This Program has unmet requirements.";
                return false;
            }

            if (!_program.MeetsTrustThreshold)
            {
                reason = $"This Program requires {_program.TrustCost:N0} to accept at this speed.";
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

            if (_program == null)
            {
                reason = "An error occurred during loading. The program field is null!";
                return false;
            }

            if (!_program.CanComplete)
            {
                if (_program.AllObjectivesMet && _program.IsActive && !_program.IsComplete)
                {
                    Debug.LogError($"[RP-0]: Program {_program.name} was incorrectly not marked as complete. Marking complete now.");
                    _program.MarkObjectivesComplete();
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
                ProgramHandler.Instance.ActivateProgram(_program);
        }

        protected override void OnUnregister()
        {
            base.OnUnregister();

            if (ProgramHandler.Instance && ProgramHandler.Instance.IsInAdmin)
                ProgramHandler.Instance.CompleteProgram(_program);
        }
    }
}
