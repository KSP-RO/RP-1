using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static ConfigNode;

namespace RP0.Milestones
{
    public class Milestone
    {
        [Persistent] public string name;
        [Persistent] public string contractName;
        [Persistent] public string programName;
        [Persistent] public string headline;
        [Persistent] public string article;
        [Persistent] public string image;

        public Milestone()
        {
        }

        public Milestone(ConfigNode n)
        {
            LoadObjectFromConfig(this, n);
        }
    }
}
