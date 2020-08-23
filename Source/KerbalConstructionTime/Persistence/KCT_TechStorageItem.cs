using System.Collections.Generic;

namespace KerbalConstructionTime
{
    public class KCT_TechStorageItem
    {
        [Persistent] string techName, techID;
        [Persistent] int scienceCost;
        [Persistent] double progress;
        [Persistent] List<string> parts;

        public TechItem ToTechItem()
        {
            var ret = new TechItem(techID, techName, progress, scienceCost, parts);
            return ret;
        }

        public KCT_TechStorageItem FromTechItem(TechItem techItem)
        {
            techName = techItem.TechName;
            techID = techItem.TechID;
            progress = techItem.Progress;
            scienceCost = techItem.ScienceCost;
            parts = techItem.UnlockedParts;

            return this;
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
