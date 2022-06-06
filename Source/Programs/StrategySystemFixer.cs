// Ported from Strategia by nightingale/jrossignol.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using KSP;
using Strategies;

namespace RP0.Programs
{
    /// <summary>
    /// Special MonoBehaviour to fix up the departments.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public class StrategySystemFixer : MonoBehaviour
    {
        public void Awake()
        {
        }

        public void Update()
        {
            if (Process())
            {
                Destroy(this);
            }
        }

        public bool Process()
        {
            // Wait for the strategy system to get loaded
            if (StrategySystem.Instance == null)
            {
                return false;
            }

            // Find the departments
            DepartmentConfig programsOffice = null;
            foreach (DepartmentConfig department in StrategySystem.Instance.SystemConfig.Departments)
            {
                // Save programs and make it the first one.
                if (department.Name == "Programs")
                {
                    programsOffice = department;
                    break;
                }
            }

            // Re-order stuff
            if (programsOffice != null)
            {
                StrategySystem.Instance.SystemConfig.Departments.Remove(programsOffice);
                StrategySystem.Instance.SystemConfig.Departments.Insert(0, programsOffice);
            }

            return true;
        }
    }
}