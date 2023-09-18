namespace RP0
{
    public interface ISpaceCenterProject
    {
        string GetItemName();
        double GetBuildRate();
        double GetFractionComplete();
        double GetTimeLeft();
        double GetTimeLeftEst(double offset);
        VesselProject.ListType GetListType();
        bool IsComplete();
        double IncrementProgress(double UTDiff);
    }
}