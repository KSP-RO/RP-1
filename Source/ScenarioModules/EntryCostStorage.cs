using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using KSP;

namespace RP0
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class EntryCostStorage : MonoBehaviour
    {
        protected static Dictionary<string, int> originalEntryCosts = new Dictionary<string, int>();

        public static int GetCost(string partName)
        {
            int tmp;
            if (originalEntryCosts.TryGetValue(partName, out tmp))
                return tmp;

            return 0;
        }

        protected bool run = true;

        public void Update()
        {
            if (run)
            {
                originalEntryCosts.Clear();

                foreach (AvailablePart ap in PartLoader.LoadedPartsList)
                    originalEntryCosts[ap.name] = ap.entryCost;

                run = false;

                GameObject.Destroy(this);
            }
        }
    }
}
