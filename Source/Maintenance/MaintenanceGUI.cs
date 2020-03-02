using System;
using UnityEngine;

namespace RP0
{
    public class MaintenanceGUI : UIBase
    {
        private Vector2 nautListScroll = new Vector2();

        private enum per { DAY, MONTH, YEAR };
        private per displayPer = per.YEAR;

        private double perFactor
        {
            get
            {
                switch (displayPer)
                {
                    case per.DAY:
                        return 1d;
                    case per.MONTH:
                        return 30d;
                    case per.YEAR:
                        return 365d;
                    default: // can't happen
                        return 0d;
                }
            }
        }

        private string perFormat => displayPer == per.DAY ? "N1" : "N0";

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
                if (GUILayout.Button("ⓘ", GUILayout.ExpandWidth(false)))
                {
                    TopWindow.SwitchTabTo(tabs.Facilities);
                }
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Integration", HighLogic.Skin.label, GUILayout.Width(160));
                GUILayout.Label((MaintenanceHandler.Instance.integrationUpkeep * perFactor).ToString(perFormat), rightLabel, GUILayout.Width(160));
                if (GUILayout.Button("ⓘ", GUILayout.ExpandWidth(false)))
                {
                    TopWindow.SwitchTabTo(tabs.Integration);
                }
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
                GUILayout.Label((MaintenanceHandler.Instance.nautTotalUpkeep * perFactor).ToString(perFormat), rightLabel, GUILayout.Width(160));
                if (GUILayout.Button("ⓘ", GUILayout.ExpandWidth(false)))
                {
                    TopWindow.SwitchTabTo(tabs.Astronauts);
                }
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("Total (after subsidy)", boldLabel, GUILayout.Width(160));
                GUILayout.Label(((MaintenanceHandler.Instance.totalUpkeep + MaintenanceHandler.Instance.settings.maintenanceOffset) * perFactor).ToString(perFormat), boldRightLabel, GUILayout.Width(160));
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
                    GUILayout.Label(String.Format("  level {0} × {1}", i + 1, MaintenanceHandler.Instance.kctPadCounts[i]), HighLogic.Skin.label, GUILayout.Width(160));
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
                GUILayout.Space(20);
                GUILayout.Label("Name", HighLogic.Skin.label, GUILayout.Width(144));
                GUILayout.Label("Retires NET", HighLogic.Skin.label, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
            foreach (string name in Crew.CrewHandler.Instance.kerbalRetireTimes.Keys) {
                GUILayout.BeginHorizontal();
                try {
                    GUILayout.Space(20);
                    double rt = Crew.CrewHandler.Instance.kerbalRetireTimes[name];
                    GUILayout.Label(name, HighLogic.Skin.label, GUILayout.Width(144));
                    GUILayout.Label(Crew.CrewHandler.Instance.retirementEnabled ? KSPUtil.PrintDate(rt, false) : "(n/a)", HighLogic.Skin.label, GUILayout.Width(160));
                } finally {
                    GUILayout.EndHorizontal();
                }
            }
        }

        public void astronautsTab()
        {
            if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
            {
                GUILayout.BeginHorizontal();
                try {
                    GUILayout.Label("Astronaut costs (per ", HighLogic.Skin.label);
                    perSelector();
                    GUILayout.Label(")", HighLogic.Skin.label);
                } finally {
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.BeginHorizontal();
            try {
                int nautCount = HighLogic.CurrentGame.CrewRoster.GetActiveCrewCount();
                GUILayout.Label(String.Format("Corps: {0:N0} astronauts", nautCount), HighLogic.Skin.label, GUILayout.Width(160));
            } finally {
                GUILayout.EndHorizontal();
            }
            nautListScroll = GUILayout.BeginScrollView(nautListScroll, GUILayout.Width(360), GUILayout.Height(280));
            try {
                nautList();
            } finally {
                GUILayout.EndScrollView();
            }
            if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
            {
                GUILayout.BeginHorizontal();
                try {
                    GUILayout.Label("Astronaut base cost", HighLogic.Skin.label, GUILayout.Width(160));
                    GUILayout.Label((MaintenanceHandler.Instance.nautBaseUpkeep * perFactor).ToString(perFormat), rightLabel, GUILayout.Width(160));
                } finally {
                    GUILayout.EndHorizontal();
                }

                GUILayout.BeginHorizontal();
                try
                {
                    GUILayout.Label("Astronaut operational cost", HighLogic.Skin.label, GUILayout.Width(160));
                    GUILayout.Label((MaintenanceHandler.Instance.nautInFlightUpkeep * perFactor).ToString(perFormat), rightLabel, GUILayout.Width(160));
                }
                finally
                {
                    GUILayout.EndHorizontal();
                }

                GUILayout.BeginHorizontal();
                try
                {
                    GUILayout.Label("Astronaut training cost", HighLogic.Skin.label, GUILayout.Width(160));
                    GUILayout.Label((MaintenanceHandler.Instance.trainingUpkeep * perFactor).ToString(perFormat), rightLabel, GUILayout.Width(160));
                }
                finally
                {
                    GUILayout.EndHorizontal();
                }

                GUILayout.BeginHorizontal();
                try {
                    GUILayout.Label("Total", boldLabel, GUILayout.Width(160));
                    GUILayout.Label((MaintenanceHandler.Instance.nautTotalUpkeep * perFactor).ToString(perFormat), boldRightLabel, GUILayout.Width(160));
                } finally {
                    GUILayout.EndHorizontal();
                }
            }
        }
    }
}

