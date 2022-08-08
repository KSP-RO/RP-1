using System;
using UnityEngine;

namespace KerbalConstructionTime
{
    public static partial class KCT_GUI
    {
        private static string _tonnageLimit = "1";
        private static string _heightLimit = "10";
        private static string _widthLimit = "2";
        private static string _lengthLimit = "2";
        private static bool _isHumanRated = false;
        private static LCItem.LCData _lcData = new LCItem.LCData();

        public static void DrawNewLCWindow(int windowID)
        {
            LCItem activeLC = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance;
            double oldVABCost = 0, oldPadCost = 0, lpMult = 1;

            bool isModify = GUIStates.ShowModifyLC;

            GUILayout.BeginVertical();
            if (isModify)
            {
                GUILayout.Label(activeLC.Name);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Tonnage limits:");
                GUILayout.Label(activeLC.SupportedMassAsPrettyText, GetLabelRightAlignStyle(), GUILayout.ExpandWidth(false));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Size limits:");
                GUILayout.Label(activeLC.SupportedSizeAsPrettyText, GetLabelRightAlignStyle(), GUILayout.ExpandWidth(false));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Human Rated:");
                GUILayout.Label(activeLC.IsHumanRated ? "Yes" : "No", GetLabelRightAlignStyle(), GUILayout.ExpandWidth(false));
                GUILayout.EndHorizontal();

                Utilities.GetPadStats(activeLC.MassMax, activeLC.SizeMax, activeLC.IsHumanRated, out oldPadCost, out oldVABCost, out _);
                lpMult = activeLC.LaunchPads.Count;
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Name:", GUILayout.ExpandWidth(false));
                _newName = GUILayout.TextField(_newName);
                GUILayout.EndHorizontal();
            }

            GUILayout.Label(isModify ? "New Limits" : "Launch Complex Limits:");

            bool isHangar = isModify && activeLC.LCType == LaunchComplexType.Hangar;
            double curPadCost = 0;
            double curVABCost = 0;
            float fractionalPadLvl = -1;
            float tonnageLimit = isHangar ? activeLC.MassMax : 0;
            int tonnageLimitInt = (int)tonnageLimit;
            float heightLimit = 0;
            float widthLimit = 0;
            float lengthLimit = 0;
            float minTonnage = 0f;
            Vector3 curPadSize = Vector3.zero;

            bool parsedTonnage = true;
            if (!isHangar)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Maximum tonnage:", GUILayout.ExpandWidth(false));
                string newStr = GUILayout.TextField(_tonnageLimit, GetTextFieldRightAlignStyle());
                if (int.TryParse(newStr, out tonnageLimitInt))
                {
                    parsedTonnage = true;
                    _tonnageLimit = tonnageLimitInt.ToString();
                }
                GUILayout.EndHorizontal();
            }

            if (parsedTonnage &&
                float.TryParse(_lengthLimit, out lengthLimit) &&
                float.TryParse(_widthLimit, out widthLimit) &&
                float.TryParse(_heightLimit, out heightLimit))
            {
                tonnageLimit = tonnageLimitInt;
                curPadSize.x = widthLimit;
                curPadSize.y = heightLimit;
                curPadSize.z = lengthLimit;
                Utilities.GetPadStats(isHangar ? activeLC.MassMax : tonnageLimit, curPadSize, isHangar ? activeLC.IsHumanRated : _isHumanRated, out curPadCost, out curVABCost, out fractionalPadLvl);
                if (!isHangar)
                    minTonnage = LCItem.CalcMassMin(tonnageLimit);
            }
            if (!isHangar)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Minimum tonnage:");
                GUILayout.Label(minTonnage.ToString("N0"), GetLabelRightAlignStyle(), GUILayout.ExpandWidth(false));
                GUILayout.EndHorizontal();

                if (isModify)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Upgrade Limit for max tng:");
                    GUILayout.Label($"{(int)(activeLC.MassOrig * 2f):N0}", GetLabelRightAlignStyle(), GUILayout.ExpandWidth(false));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Downgrade Limit for max tng:");
                    GUILayout.Label($"{Math.Max(1, (int)(activeLC.MassOrig * 0.5f)):N0}", GetLabelRightAlignStyle(), GUILayout.ExpandWidth(false));
                    GUILayout.EndHorizontal();

                    
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Upgrade Limit for max tng:");
                    GUILayout.Label($"{Math.Max(3, tonnageLimitInt * 2):N0}", GetLabelRightAlignStyle(), GUILayout.ExpandWidth(false));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Downgrade Limit for max tng:");
                    GUILayout.Label($"{Math.Max(1, tonnageLimit / 2):N0}", GetLabelRightAlignStyle(), GUILayout.ExpandWidth(false));
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Length limit:", GUILayout.ExpandWidth(false));
            _lengthLimit = GUILayout.TextField(_lengthLimit, GetTextFieldRightAlignStyle());
            GUILayout.Label("m", GetLabelRightAlignStyle(), GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Width limit:", GUILayout.ExpandWidth(false));
            _widthLimit = GUILayout.TextField(_widthLimit, GetTextFieldRightAlignStyle());
            GUILayout.Label("m", GetLabelRightAlignStyle(), GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Height limit:", GUILayout.ExpandWidth(false));
            _heightLimit = GUILayout.TextField(_heightLimit, GetTextFieldRightAlignStyle());
            GUILayout.Label("m", GetLabelRightAlignStyle(), GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();

            if (!isModify || activeLC.LCType == LaunchComplexType.Pad)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(" ");
                _isHumanRated = GUILayout.Toggle(_isHumanRated, "Human-Rated", GUILayout.ExpandWidth(false));
                GUILayout.EndHorizontal();
            }

            double totalCost = 0;
            if (!isHangar)
            {
                if (curPadCost > oldPadCost)
                    totalCost = curPadCost - oldPadCost;
                else
                    totalCost = (oldPadCost - curPadCost) * 0.5d;
                // Modify case: Additional pads cost less to build, so cost less to modify.
                if (lpMult > 1)
                    totalCost *= 1d + (lpMult - 1d) * PresetManager.Instance.ActivePreset.GeneralSettings.AdditionalPadCostMult;
            }

            if (isModify)
            {
                // Enforce a min cost for pad size changes
                const double minPadModifyCost = 1000d;
                if (!isHangar && activeLC.MassMax != tonnageLimit && totalCost < minPadModifyCost)
                    totalCost = minPadModifyCost;

                double heightAbs = Math.Abs(heightLimit - activeLC.SizeMax.y);
                double costScalingFactor = isHangar ? 500d : UtilMath.LerpUnclamped(100d, 1000d, UtilMath.InverseLerp(10d, 55d, UtilMath.Clamp(activeLC.MassOrig, 10d, 50d)));
                double renovateCost = Math.Abs(curVABCost - oldVABCost)
                    + heightAbs * costScalingFactor
                    + Math.Abs(widthLimit - activeLC.SizeMax.x) * costScalingFactor * 0.5d
                    + Math.Abs(lengthLimit - activeLC.SizeMax.z) * costScalingFactor * 0.5d;

                //// moving the roof
                //if (heightAbs > 0.1d)
                //    renovateCost += 3000d;

                if (curVABCost < oldVABCost)
                    renovateCost *= 0.5d;

                if (curVABCost > oldVABCost && renovateCost > curVABCost)
                    renovateCost = curVABCost;

                totalCost += renovateCost;
            }
            else
            {
                totalCost += curVABCost;
            }

            if (totalCost > 0)
            {
                double totalCostForMaintenance = curVABCost;
                if(!isHangar)
                    totalCostForMaintenance += curPadCost * lpMult;

                // Additional pads cost less
                curPadCost *= PresetManager.Instance.ActivePreset.GeneralSettings.AdditionalPadCostMult;

                GUILayout.BeginHorizontal();
                GUILayout.Label("Max Engineers:", GUILayout.ExpandWidth(false));
                GUILayout.Label($"{LCItem.MaxEngineersCalc(tonnageLimit, curPadSize, _isHumanRated):N0}", GetLabelRightAlignStyle());
                GUILayout.EndHorizontal();

                GUILayout.Label(" ");

                double buildTime = ConstructionBuildItem.CalculateBuildTime(totalCost, SpaceCenterFacility.LaunchPad, null);
                string sBuildTime = KSPUtil.PrintDateDelta(buildTime, includeTime: false);
                string costString = isModify ? "Renovate Cost:" : "Build Cost:";
                GUILayout.BeginHorizontal();
                GUILayout.Label(costString, GUILayout.ExpandWidth(false));
                GUILayout.Label($"√{-RP0.CurrencyUtils.Funds(RP0.TransactionReasonsRP0.StructureConstructionLC, -totalCost):N0}", GetLabelRightAlignStyle());
                GUILayout.EndHorizontal();
                if (!isModify || activeLC.LCType == LaunchComplexType.Pad)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Extra Pad Cost:", GUILayout.ExpandWidth(false));
                    GUILayout.Label($"√{-RP0.CurrencyUtils.Funds(RP0.TransactionReasonsRP0.StructureConstructionLC, -curPadCost):N0}", GetLabelRightAlignStyle());
                    GUILayout.EndHorizontal();
                }
                GUILayout.BeginHorizontal();
                GUILayout.Label("Est. construction time:", GUILayout.ExpandWidth(false));
                GUILayout.Label(new GUIContent(sBuildTime, isModify ? "With current construction engineers" : "With LC engineers as\nconstruction engineers"), GetLabelRightAlignStyle());
                GUILayout.EndHorizontal();

                double projectedMaintenance = RP0.MaintenanceHandler.Instance.ComputeDailyMaintenanceCost(totalCostForMaintenance, isHangar ? RP0.FacilityMaintenanceType.Hangar : RP0.FacilityMaintenanceType.LC);

                if (projectedMaintenance > 0d)
                {
                    projectedMaintenance = -RP0.CurrencyUtils.Funds(RP0.TransactionReasonsRP0.StructureRepairLC, -projectedMaintenance);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Est. Yearly Upkeep:", GUILayout.ExpandWidth(false));
                    GUILayout.Label(new GUIContent((projectedMaintenance * 365.25d).ToString("N0"), $"Daily: {projectedMaintenance:N1}"), GetLabelRightAlignStyle());
                    GUILayout.EndHorizontal();
                }
            }

            if (!isHangar)
            {
                _lcData.massMax = tonnageLimit;
                _lcData.sizeMax = curPadSize;
                _lcData.isHumanRated = _isHumanRated;
                LCEfficiency closestEff = LCEfficiency.FindClosest(_lcData, out double closeness);

                GUILayout.BeginHorizontal();
                GUILayout.Label("Closest Existing LC:", GUILayout.ExpandWidth(false));
                GUILayout.Label(closeness > 0 ? closestEff.FirstLC().Name : "None", GetLabelRightAlignStyle());
                GUILayout.EndHorizontal();
                    
                GUILayout.BeginHorizontal();
                GUILayout.Label("Commonality:");
                GUILayout.Label(closeness.ToString("P0"), GetLabelRightAlignStyle());
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                if (closeness == 1d)
                {
                    GUILayout.Label("Uses shared efficiency.");
                }
                else
                {
                    GUILayout.Label("Starting Efficiency:");
                    GUILayout.Label($"{(closeness > 0 ? Math.Max(LCEfficiency.MinEfficiency, closestEff.PostClosenessStartingEfficiency(closeness)) : LCEfficiency.MinEfficiency):P1}", GetLabelRightAlignStyle());
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(isModify ? "Renovate" : "Build") && ValidateLCCreationParameters(_newName, fractionalPadLvl, tonnageLimit, curPadSize, isModify ? activeLC : null))
            {
                KCTGameStates.StarterLCBuilding |= !isModify;

                string lcName = isModify ? activeLC.Name : _newName;
                if (!Utilities.CurrentGameIsCareer())
                {
                    KCTDebug.Log($"Building/Modifying launch complex {lcName}");
                    if (isModify)
                        activeLC.Modify(new LCItem.LCData(activeLC.Name, tonnageLimit, activeLC.MassOrig, curPadSize, activeLC.LCType, _isHumanRated), Guid.NewGuid());
                    else
                        KCTGameStates.ActiveKSC.LaunchComplexes.Add(new LCItem(_newName, tonnageLimit, tonnageLimit, curPadSize, LaunchComplexType.Pad, _isHumanRated, KCTGameStates.ActiveKSC));
                }
                else
                {
                    KCTDebug.Log($"Building/Modifying launch complex {lcName}");
                    LCItem lc;
                    if (isModify)
                    {
                        lc = activeLC;
                        Utilities.ChangeEngineers(lc, -lc.Engineers);
                        KCTGameStates.ActiveKSC.SwitchToPrevLaunchComplex();
                    }
                    else
                    {
                        lc = new LCItem(_newName, tonnageLimit, Math.Max(1.5f, tonnageLimit), curPadSize, LaunchComplexType.Pad, _isHumanRated, KCTGameStates.ActiveKSC);
                        KCTGameStates.ActiveKSC.LaunchComplexes.Add(lc);
                    }
                    lc.IsOperational = false;

                    var lcConstr = new LCConstruction
                    {
                        LCID = lc.ID,
                        Cost = totalCost,
                        Name = lcName,
                        IsModify = isModify,
                        ModID = isModify ? Guid.NewGuid() : lc.ModID,
                        LCData = new LCItem.LCData(lc.Name, tonnageLimit, lc.MassOrig, curPadSize, lc.LCType, _isHumanRated)
                    };
                    lcConstr.SetBP(totalCost);
                    KCTGameStates.ActiveKSC.LCConstructions.Add(lcConstr);

                    try
                    {
                        KCTEvents.OnLCConstructionQueued?.Fire(lcConstr, lc);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
                GUIStates.ShowNewLC = false;
                GUIStates.ShowModifyLC = false;
                _centralWindowPosition.height = 1;
                _centralWindowPosition.width = 300;
                _centralWindowPosition.x = (Screen.width - 300) / 2;
                if (!HighLogic.LoadedSceneIsEditor)
                    GUIStates.ShowBuildList = true;
            }

            if (GUILayout.Button("Cancel"))
            {
                _centralWindowPosition.height = 1;
                _centralWindowPosition.width = 150;
                _centralWindowPosition.x = (Screen.width - 150) / 2;
                GUIStates.ShowNewLC = false;
                GUIStates.ShowModifyLC = false;
                if (!HighLogic.LoadedSceneIsEditor)
                    GUIStates.ShowBuildList = true;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            CenterWindow(ref _centralWindowPosition);
        }

        private static bool ValidateLCCreationParameters(string newName, float fractionalPadLvl, float tonnageLimit, Vector3 curPadSize, LCItem lc)
        {
            if (curPadSize == Vector3.zero)
            {
                ScreenMessages.PostScreenMessage("Please enter a valid size");
                return false;
            }

            if (lc != null && lc.LCType == LaunchComplexType.Hangar)
                return true;

            if (fractionalPadLvl == -1 || tonnageLimit == 0 || (lc != null && (tonnageLimit < Math.Max(1, (int)lc.MassOrig / 2) || tonnageLimit > lc.MassOrig * 2)))
            {
                ScreenMessages.PostScreenMessage("Please enter a valid tonnage limit");
                return false;
            }

            // Don't bother with name if it's a modify.
            if (lc != null)
                return true;

            if (string.IsNullOrEmpty(_newName))
            {
                ScreenMessages.PostScreenMessage("Enter a name for the new launch complex");
                return false;
            }

            for (int i = 0; i < KCTGameStates.ActiveKSC.LaunchComplexes.Count; i++)
            {
                var lp = KCTGameStates.ActiveKSC.LaunchComplexes[i];
                if (string.Equals(lp.Name, _newName, StringComparison.OrdinalIgnoreCase))
                {
                    ScreenMessages.PostScreenMessage("Another launch complex with the same name already exists");
                    return false;
                }
            }

            return true;
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
