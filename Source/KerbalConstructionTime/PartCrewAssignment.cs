using System.Collections.Generic;

namespace KerbalConstructionTime
{
    public class PartCrewAssignment : IConfigNode
    {
        [Persistent]
        public List<CrewMemberAssignment> CrewList = new List<CrewMemberAssignment>();
        [Persistent]
        public uint PartID;

        public PartCrewAssignment()
        {
        }

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

        public void Load(ConfigNode node)
        {
            ConfigNode.LoadObjectFromConfig(this, node);
        }

        public void Save(ConfigNode node)
        {
            ConfigNode.CreateConfigFromObject(this, node);
        }
    }

    public class CrewMemberAssignment
    {
        public ProtoCrewMember PCM 
        {
            get
            {
                if (_pcm == null && _pcmName != string.Empty)
                    _pcm = HighLogic.CurrentGame.CrewRoster[_pcmName];
                return _pcm;
            }
            set
            {
                _pcm = value;
                if (value == null)
                    _pcmName = string.Empty;
                else
                    _pcmName = value.name;
            }
        }
        private ProtoCrewMember _pcm;
        [Persistent]
        private string _pcmName = string.Empty;
        [Persistent]
        public bool HasChute;
        [Persistent]
        public bool HasJetpack;

        public CrewMemberAssignment()
        {
        }

        public CrewMemberAssignment(ProtoCrewMember pcm)
        {
            PCM = pcm;
        }
    }
}
