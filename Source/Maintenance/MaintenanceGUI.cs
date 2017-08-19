using System;
using UnityEngine;

namespace RP0
{
    class MaintenanceGUI : UIBase
    {
        private Vector2 nautListScroll = new Vector2();

        private enum per { DAY, MONTH, YEAR };
        private per displayPer = per.YEAR;

        private double perFactor { get {
            switch (displayPer) {
            case per.DAY:
                return 1d;
            case per.MONTH:
                return 30d;
            case per.YEAR:
                return 365d;
            default: // can't happen
                return 0d;
            }
        }}
        private string perFormat { get {
            if (displayPer == per.DAY)
                return "N1";
            return "N0";
        }}

        private void perSelector()
        {
            GUILayout.BeginHorizontal();
            try {
                if (toggleButton("Day", displayPer == per.DAY))
                    displayPer = per.DAY;
                if (toggleButton("Month", displayPer == per.MONTH))
                    displayPer = per.MONTH;
                if (toggleButton("Year", displayPer == per.YEAR))
                    displayPer = per.YEAR;
            } finally {
                GUILayout.EndHorizontal();
            }
        }

        public void summaryTab()
        {
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Maintenance costs (per ", HighLogic.Skin.label);
                perSelector();
                GUILayout.Label(")", HighLogic.Skin.label);
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Facilities", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.facilityUpkeep * perFactor).ToString(perFormat), rightLabel, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Integration", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.integrationUpkeep * perFactor).ToString(perFormat), rightLabel, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Research Teams", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.researchUpkeep * perFactor).ToString(perFormat), rightLabel, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Astronauts", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.nautUpkeep * perFactor).ToString(perFormat), rightLabel, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Total", boldLabel, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.totalUpkeep * perFactor).ToString(perFormat), boldRightLabel, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
        }

        public void facilitiesTab()
        {
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Facilities costs (per ", HighLogic.Skin.label);
                perSelector();
                GUILayout.Label(")", HighLogic.Skin.label);
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Launch Pads", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.padCost * perFactor).ToString(perFormat), rightLabel, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
            for (int i = 0; i < MaintenanceHandler.padLevels; i++) {
                if (MaintenanceHandler.Instance.padCosts[i] == 0d)
                    continue;
                GUILayout.BeginHorizontal();
                try {
                    GUILayout.Label(String.Format("  level {0} × {1}", i, MaintenanceHandler.Instance.kctPadCounts[i]), HighLogic.Skin.label, GUILayout.Width(160));
                    GUILayout.Label((MaintenanceHandler.Instance.padCosts[i] * perFactor).ToString(perFormat), rightLabel, GUILayout.Width(160));
                } finally {
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Runway", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.runwayCost * perFactor).ToString(perFormat), rightLabel, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Vertical Assembly Building", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.vabCost * perFactor).ToString(perFormat), rightLabel, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Spaceplane Hangar", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.sphCost * perFactor).ToString(perFormat), rightLabel, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Research & Development", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.rndCost * perFactor).ToString(perFormat), rightLabel, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Mission Control", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.mcCost * perFactor).ToString(perFormat), rightLabel, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Tracking Station", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.tsCost * perFactor).ToString(perFormat), rightLabel, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Astronaut Complex", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.acCost * perFactor).ToString(perFormat), rightLabel, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Total", boldLabel, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.facilityUpkeep * perFactor).ToString(perFormat), boldRightLabel, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
        }

        public void integrationTab()
        {
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Integration Team costs (per ", HighLogic.Skin.label);
                perSelector();
                GUILayout.Label(")", HighLogic.Skin.label);
            } finally {
                GUILayout.EndHorizontal();
            }
            foreach (string site in MaintenanceHandler.Instance.kctBuildRates.Keys) {
                double rate = MaintenanceHandler.Instance.kctBuildRates[site];
                GUILayout.BeginHorizontal();
                try {
                    GUILayout.Label(site, HighLogic.Skin.label, GUILayout.Width(160));
                    GUILayout.Label((rate * MaintenanceHandler.Instance.settings.kctBPMult * perFactor).ToString(perFormat), rightLabel, GUILayout.Width(160));
                } finally {
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Total", boldLabel, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.integrationUpkeep * perFactor).ToString(perFormat), boldRightLabel, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
        }

        private void nautList()
        {
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Space(40);
                GUILayout.Label("Name", HighLogic.Skin.label, GUILayout.Width(120));
                GUILayout.Label("Retires NET", HighLogic.Skin.label, GUILayout.Width(80));
            } finally {
                GUILayout.EndHorizontal();
            }
            foreach (string name in Crew.CrewHandler.Instance.kerbalRetireTimes.Keys) {
                GUILayout.BeginHorizontal();
                try {
                    GUILayout.Space(40);
                    double rt = Crew.CrewHandler.Instance.kerbalRetireTimes[name];
                    GUILayout.Label(name, HighLogic.Skin.label, GUILayout.Width(120));
                    GUILayout.Label(KSPUtil.PrintDate(rt, false), HighLogic.Skin.label, GUILayout.Width(80));
                } finally {
                    GUILayout.EndHorizontal();
                }
            }
        }

        public void astronautsTab()
        {
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Astronaut costs (per ", HighLogic.Skin.label);
                perSelector();
                GUILayout.Label(")", HighLogic.Skin.label);
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                int nautCount = HighLogic.CurrentGame.CrewRoster.GetActiveCrewCount();
                GUILayout.Label(String.Format("Corps: {0:N0} astronauts", nautCount), HighLogic.Skin.label, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
            nautListScroll = GUILayout.BeginScrollView(nautListScroll, GUILayout.Width(280), GUILayout.Height(144));
            try {
                nautList();
            } finally {
                GUILayout.EndScrollView();
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Cost per astronaut", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.nautYearlyUpkeep * perFactor / 365d).ToString(perFormat), rightLabel, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Total", boldLabel, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.nautUpkeep * perFactor).ToString(perFormat), boldRightLabel, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
        }
    }
}

