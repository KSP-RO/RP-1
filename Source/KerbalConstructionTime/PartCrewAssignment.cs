using System.Collections.Generic;

namespace KerbalConstructionTime
{
    public class PartCrewAssignment
    {
        public List<CrewMemberAssignment> CrewList { get; set; }
        public uint PartID { get; set; }

        public PartCrewAssignment(uint ID, List<CrewMemberAssignment> crew)
        {
            PartID = ID;
            CrewList = crew;
        }

        public PartCrewAssignment (Part part, List<CrewMemberAssignment> crew)
        {
            PartID = part.flightID;
            CrewList = crew;
        }
    }

    public class CrewMemberAssignment
    {
        public ProtoCrewMember PCM { get; set; }
        public bool HasChute { get; set; }
        public bool HasJetpack { get; set; }

        public CrewMemberAssignment()
        {
        }

        public CrewMemberAssignment(ProtoCrewMember pcm)
        {
            PCM = pcm;
        }
    }
}
