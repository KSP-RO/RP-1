namespace KerbalConstructionTime
{
    public interface IKCTBuildItem
    {
        string GetItemName();
        double GetBuildRate();
        double GetFractionComplete();
        double GetTimeLeft();
        double GetTimeLeftEst(double offset);
        BuildListVessel.ListType GetListType();
        bool IsComplete();
        double IncrementProgress(double UTDiff);
    }
}