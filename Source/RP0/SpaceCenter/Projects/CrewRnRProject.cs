using System;

namespace RP0
{
    /// <summary>
    /// Lightweight, non-persisted wrapper that lets an astronaut's post-flight R&R period
    /// show up in the build list alongside the other timed projects, so the player
    /// can see when a naut becomes available to train again. These are created on the fly
    /// when rendering the build list and are never stored.
    /// </summary>
    public class CrewRnRProject : ISpaceCenterProject
    {
        private readonly ProtoCrewMember _pcm;

        public CrewRnRProject(ProtoCrewMember pcm)
        {
            _pcm = pcm;
        }

        public string GetItemName() => $"{_pcm.displayName} (R&R)";

        public double GetBuildRate() => 1d;

        public double GetFractionComplete() => 0d;

        public double GetTimeLeft() => Math.Max(0d, _pcm.inactiveTimeEnd - Planetarium.GetUniversalTime());

        public double GetTimeLeftEst(double offset) => GetTimeLeft();

        public ProjectType GetProjectType() => ProjectType.CrewRnR;

        public bool IsComplete() => !_pcm.inactive;

        public double IncrementProgress(double UTDiff) => 0d;
    }
}
