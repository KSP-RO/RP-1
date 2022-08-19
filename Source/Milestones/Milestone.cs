using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static ConfigNode;

namespace RP0.Milestones
{
    public class Milestone : IConfigNode
    {
        [Persistent] public string name;
        [Persistent(isPersistant = false)] public string headline;
        [Persistent(isPersistant = false)] public string article;
        [Persistent(isPersistant = false)] public string image;
        [Persistent(isPersistant = false)] public double date;

        public Milestone()
        {
        }

        public Milestone(ConfigNode n) : this()
        {
            Load(n);
        }

        public void Load(ConfigNode node)
        {
            LoadObjectFromConfig(this, node);

            name = node.name;
            node.TryGetValue("headline", ref headline);
            node.TryGetValue("article", ref article);
            node.TryGetValue("image", ref image);
            date = 0f;
        }

        public void Save(ConfigNode node)
        {
            CreateConfigFromObject(this, node);
        }

    }
}
