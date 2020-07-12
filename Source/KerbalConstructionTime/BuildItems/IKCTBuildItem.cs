namespace KerbalConstructionTime
{
    public interface IKCTBuildItem
    {
        string GetItemName();
        double GetBuildRate();
        double GetTimeLeft();
        BuildListVessel.ListType GetListType();
        bool IsComplete();
        void IncrementProgress(double UTDiff);
    }
}

/*
    KerbalConstructionTime (c) by Michael Marvin, Zachary Eck

    KerbalConstructionTime is licensed under a
    Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.

    You should have received a copy of the license along with this
    work. If not, see <http://creativecommons.org/licenses/by-nc-sa/4.0/>.
*/
