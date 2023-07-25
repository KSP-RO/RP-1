using static ConfigNode;

namespace RP0.Milestones
{
    public class Milestone
    {
        [Persistent] public string name;
        [Persistent] public string contractName;
        [Persistent] public string programName;
        [Persistent] public string screenshotContractParamName;
        [Persistent] public string headline;
        [Persistent] public string article;
        [Persistent] public string image;
        [Persistent] public bool canRequeue = false;

        public Milestone()
        {
        }

        public Milestone(ConfigNode n)
        {
            LoadObjectFromConfig(this, n);
        }
    }
}
