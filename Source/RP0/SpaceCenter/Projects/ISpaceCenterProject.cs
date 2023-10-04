namespace RP0
{
    public enum ProjectType
    {
        None,
        VAB,
        SPH,
        TechNode,
        Reconditioning,
        KSC,
        AirLaunch,
        Crew
    };

    public interface ISpaceCenterProject
    {
        string GetItemName();
        double GetBuildRate();
        double GetFractionComplete();
        double GetTimeLeft();
        double GetTimeLeftEst(double offset);
        ProjectType GetProjectType();
        bool IsComplete();
        double IncrementProgress(double UTDiff);
    }
}