using System;
using System.Collections.Generic;
using UnityEngine;
using ROUtils;

namespace RP0
{
    public static partial class KCT_GUI
    {
        private static readonly GUIContent _extraPadsContent = new GUIContent("Extra Pad Cost:", "Cost to build additional pads past the first once LC is constructed");
        private static readonly GUIContent _cleanButtonContent = new GUIContent("Clean", "Set the LC stats to support only the current vessel, removing support for other vessels to reduce maintenance costs in exchange for a longer renovation time");
        private static readonly GUIContent _existingButtonContent = new GUIContent("Existing", "Set the LC stats to match the currently existing LC");
        private static string _tonnageLimit = "1";
        private static string _heightLimit = "10";
        private static string _widthLimit = "2";
        private static string _lengthLimit = "2";
        private static LCData _newLCData = new LCData();
        private static int _resourceCount = 0;
        private static List<string> _allResourceKeys = new List<string>();
        private static List<string> _allResourceValues = new List<string>();
        private static Vector2 _resourceListScroll = new Vector2();
        private static bool _overrideShowBuildPlans = false;
        private static float _requiredTonnage = 0;
        private static bool _assignEngOnComplete = true;

        private const double _MinResourceVolume = 250d;

        private static void SetStrings()
        {
            _tonnageLimit = _newLCData.massMax.ToString("F0");
            _heightLimit = _newLCData.sizeMax.y.ToString("F0");
            _widthLimit = _newLCData.sizeMax.x.ToString("F0");
            _lengthLimit = _newLCData.sizeMax.z.ToString("F0");
        }

        private static void SetResources()
        {
            if (_resourceCount == 0) GetAllResourceKeys();
            for (int i = 0; i < _resourceCount; i++)
            {
                if (_newLCData.resourcesHandled.TryGetValue(_allResourceKeys[i], out double resourceValue))
                {
                    _allResourceValues[i] = Math.Ceiling(resourceValue).ToString("F0");
                }
                else
                {
                    _allResourceValues[i] = "0";
                }
            }
        }

        private static void SetFieldsFromLC(LaunchComplex LC)
        {
            _newLCData.SetFrom(LC);
            SetStrings();
            SetResources();
        }

        private static void SetFieldsFromVessel(VesselProject vp, LaunchComplex lc = null)
        {
            _newLCData.sizeMax.z = Mathf.Ceil(vp.ShipSize.z * 1.2f);
            _newLCData.sizeMax.x = Mathf.Ceil(vp.ShipSize.x * 1.2f);
            _newLCData.sizeMax.y = Mathf.Ceil(vp.ShipSize.y * 1.1f);
            _newLCData.isHumanRated = vp.humanRated;
            if (lc == null)
            {
                _newLCData.massMax = Mathf.Ceil(vp.mass * 1.1f);
                if (vp.mass < 1f) // special case
                    _newLCData.massMax = 1f;

                _newLCData.Name = _newName = $"Launch Complex {(SpaceCenterManagement.Instance.ActiveSC.LaunchComplexes.Count)}";
                _newLCData.massOrig = _newLCData.massMax;
                _newLCData.lcType = LaunchComplexType.Pad;
            }
            else
            {
                _newLCData.Name = _newName = lc.Name;
                _newLCData.massOrig = lc.Stats.massOrig;
                _newLCData.lcType = lc.LCType;
                if (_newLCData.lcType != LaunchComplexType.Pad)
                {
                    _newLCData.massMax = lc.Stats.massMax;
                }
                else
                {
                    _newLCData.massMax = Mathf.Ceil(vp.mass * 1.1f);

                    if (_newLCData.massMax > _newLCData.MaxPossibleMass)
                    {
                        if (vp.mass <= _newLCData.MaxPossibleMass)
                            _newLCData.massMax = _newLCData.MaxPossibleMass;
                    }
                    else if (_newLCData.massMax < _newLCData.MinPossibleMass)
                    {
                        _newLCData.massMax = lc.MassMax;
                        if (vp.mass < _newLCData.MassMin)
                        {
                            _newLCData.massMax = _newLCData.MinPossibleMass;
                            if (vp.mass < _newLCData.MassMin)
                            {
                                // oh well. Set what we want, and let the user see the validate fail.
                                _newLCData.massMax = Mathf.Ceil((float)(vp.mass * 1.1d));
                            }
                        }
                    }
                    if (vp.mass < 1f) // special case
                        _newLCData.massMax = 1f;
                }
            }

            _newLCData.resourcesHandled.Clear();
            foreach (var kvp in vp.resourceAmounts)
            {
                if (kvp.Value * PartResourceLibrary.Instance.GetDefinition(kvp.Key).density > vp.GetTotalMass() * Formula.ResourceValidationRatioOfVesselMassMin)
                    _newLCData.resourcesHandled.Add(kvp.Key, Math.Max(_MinResourceVolume, Math.Ceiling(kvp.Value * 1.1d)));
            }
            SetStrings();
            SetResources();
        }

        private static void SetFieldsFromVesselKeepOld(VesselProject vp, LaunchComplex lc)
        {
            if (lc == null)
            {
                if (vp.mass < _newLCData.MassMin && LCData.CalcMassMaxFromMin(vp.mass) < _requiredTonnage)
                    return;
                _newLCData.isHumanRated |= vp.humanRated;
            }
            else
            {
                SetFieldsFromLC(lc);
            }

            if(_newLCData.sizeMax.z < vp.ShipSize.z)
                _newLCData.sizeMax.z = Mathf.Ceil(vp.ShipSize.z * 1.1f);
            if (_newLCData.sizeMax.x < vp.ShipSize.x)
                _newLCData.sizeMax.x = Mathf.Ceil(vp.ShipSize.x * 1.1f);
            if (_newLCData.sizeMax.y < vp.ShipSize.y)
                _newLCData.sizeMax.y = Mathf.Ceil(vp.ShipSize.y * 1.1f);

            if (_newLCData.massMax < vp.mass)
            {
                float desiredMass = Mathf.Ceil(vp.mass * 1.1f);
                _newLCData.massMax = Math.Min(desiredMass, _newLCData.MaxPossibleMass);
                // If we still fail, just set it.
                if (_newLCData.massMax < vp.mass)
                    _newLCData.massMax = desiredMass;
            }
            else if (_newLCData.MassMin > vp.mass)
            {
                _newLCData.massMax = Math.Max(_newLCData.MinPossibleMass, LCData.CalcMassMaxFromMin(vp.mass * 0.9f));
                if (vp.mass < _newLCData.MassMin)
                {
                    _newLCData.massMax = _newLCData.MinPossibleMass;
                    if (vp.mass < _newLCData.MassMin)
                    {
                        // oh well. Set what we want, and let the user see the validate fail.
                        _newLCData.massMax = Mathf.Ceil((float)(vp.mass * 1.1d));
                    }
                }
            }

            if (lc != null)
                _requiredTonnage = _newLCData.massMax;

            _newLCData.isHumanRated |= vp.humanRated;
            foreach (var kvp in vp.resourceAmounts)
            {
                if (kvp.Value * PartResourceLibrary.Instance.GetDefinition(kvp.Key).density > vp.GetTotalMass() * Formula.ResourceValidationRatioOfVesselMassMin)
                {
                    _newLCData.resourcesHandled.TryGetValue(kvp.Key, out double oldAmount);
                    if (oldAmount < kvp.Value)
                        _newLCData.resourcesHandled[kvp.Key] = Math.Max(_MinResourceVolume, Math.Ceiling(kvp.Value * 1.1d));
                }
            }
            // Reset based on our new values.
            SetStrings();
            SetResources();
        }

        private static void GetAllResourceKeys()
        {
            foreach (var kvp in Database.ResourceInfo.LCResourceTypes)
            {
                if((kvp.Value & LCResourceType.Fuel) != 0 
                    && (kvp.Value & (_newLCData.lcType == LaunchComplexType.Hangar ? LCResourceType.HangarIgnore : LCResourceType.PadIgnore)) == 0)
                {
                    _allResourceKeys.Add(kvp.Key);
                    _allResourceValues.Add(string.Empty);
                    ++_resourceCount;
                }
            }
        }

        public static void DrawNewLCWindow(int windowID)
        {
            LaunchComplex activeLC = SpaceCenterManagement.Instance.ActiveSC.ActiveLC;
            double oldVABCost = 0, oldPadCost = 0, oldResCost = 0, lpMult = 1;

            bool isModify = GUIStates.ShowModifyLC;
            bool isHangar = isModify && activeLC.LCType == LaunchComplexType.Hangar;

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

                activeLC.Stats.GetCostStats(out oldPadCost, out oldVABCost, out oldResCost); // we'll compute resource cost delta ourselves
                lpMult = isHangar ? 0d : activeLC.LaunchPadCount; // i.e. skip pads that are building, since we'll change their PadConstructions

                GUILayout.BeginHorizontal();
                GUILayout.Label("Max Engineers:", GUILayout.ExpandWidth(false));
                GUILayout.Label($"{activeLC.MaxEngineers:N0}", GetLabelRightAlignStyle());
                GUILayout.EndHorizontal();

                double oldMaintenance = Math.Max(0d, MaintenanceHandler.Instance.ComputeDailyMaintenanceCost(oldVABCost + oldResCost + oldPadCost * lpMult, isHangar ? FacilityMaintenanceType.Hangar : FacilityMaintenanceType.LC));

                oldMaintenance = -CurrencyUtils.Funds(TransactionReasonsRP0.StructureRepairLC, -oldMaintenance);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Yearly Upkeep:", GUILayout.ExpandWidth(false));
                GUILayout.Label(new GUIContent($"√{(oldMaintenance * 365.25d):N0}", $"Daily: √{oldMaintenance:N1}"), GetLabelRightAlignStyle());
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Name:", GUILayout.ExpandWidth(false));
                _newLCData.Name = GUILayout.TextField(_newLCData.Name);
                GUILayout.EndHorizontal();
            }

            GUILayout.Label(isModify ? "New Limits" : "Launch Complex Limits:");

            
            if (isHangar)
                _newLCData.isHumanRated = true;

            double curPadCost = 0;
            double curVABCost = 0;
            double curResCost = 0;
            _newLCData.massMax = isHangar ? activeLC.MassMax : 0;
            float minTonnage = 0f;

            bool parsedTonnage = true;
            if (!isHangar)
            {
                parsedTonnage = false;

                GUILayout.BeginHorizontal();
                GUILayout.Label("Maximum tonnage:", GUILayout.ExpandWidth(false));
                _tonnageLimit = GUILayout.TextField(_tonnageLimit, GetTextFieldRightAlignStyle()).Replace(",", string.Empty).Replace(".", string.Empty);
                if (float.TryParse(_tonnageLimit, out _newLCData.massMax))
                {
                    parsedTonnage = true;
                }
                GUILayout.EndHorizontal();
            }

            if (parsedTonnage &&
                float.TryParse(_lengthLimit, out _newLCData.sizeMax.z) &&
                float.TryParse(_widthLimit, out _newLCData.sizeMax.x) &&
                float.TryParse(_heightLimit, out _newLCData.sizeMax.y))
            {
                _newLCData.GetCostStats(out curPadCost, out curVABCost, out curResCost);

                if (!isHangar)
                    minTonnage = _newLCData.MassMin;
            }
            if (!isHangar)
            {
                bool isMinBad = HighLogic.LoadedSceneIsEditor && SpaceCenterManagement.Instance.EditorVessel.mass < minTonnage;
                GUILayout.BeginHorizontal();
                if (isMinBad)
                    GUILayout.Label("Minimum tonnage:", GetLabelStyleYellow());
                else
                    GUILayout.Label("Minimum tonnage:");
                GUILayout.Label(minTonnage.ToString("N0"),
                    (isMinBad
                    ? GetLabelRightAlignStyleYellow()
                    : GetLabelRightAlignStyle()), GUILayout.ExpandWidth(false));
                GUILayout.EndHorizontal();

                if (isModify)
                {
                    _newLCData.massOrig = isModify ? activeLC.MassOrig : 0;
                    int maxMax = (int)_newLCData.MaxPossibleMass;
                    int maxMin = (int)_newLCData.MinPossibleMass;
                    GUILayout.BeginHorizontal();
                    if (maxMax < _newLCData.massMax)
                        GUILayout.Label("Upgrade Limit for max tng:", GetLabelStyleYellow());
                    else
                        GUILayout.Label("Upgrade Limit for max tng:");
                    GUILayout.Label($"{maxMax:N0}", 
                        maxMax < _newLCData.massMax ? GetLabelRightAlignStyleYellow() : GetLabelRightAlignStyle(), 
                        GUILayout.ExpandWidth(false));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    if (maxMin > _newLCData.massMax)
                        GUILayout.Label("Downgrade Limit for max tng:", GetLabelStyleYellow());
                    else
                        GUILayout.Label("Downgrade Limit for max tng:");
                    GUILayout.Label($"{maxMin:N0}",
                        maxMin > _newLCData.massMax ? GetLabelRightAlignStyleYellow() : GetLabelRightAlignStyle(), 
                        GUILayout.ExpandWidth(false));
                    GUILayout.EndHorizontal();
                }
                else
                {
                    _newLCData.massOrig = _newLCData.massMax;
                    int maxMax = (int)_newLCData.MaxPossibleMass;
                    int maxMin = (int)_newLCData.MinPossibleMass;

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Upgrade Limit for max tng:");
                    GUILayout.Label($"{_newLCData.MaxPossibleMass:N0}", GetLabelRightAlignStyle(), GUILayout.ExpandWidth(false));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Downgrade Limit for max tng:");
                    GUILayout.Label($"{_newLCData.MinPossibleMass:N0}", GetLabelRightAlignStyle(), GUILayout.ExpandWidth(false));
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Length limit:", GUILayout.ExpandWidth(false));
            _lengthLimit = GUILayout.TextField(_lengthLimit, GetTextFieldRightAlignStyle()).Replace(",", string.Empty).Replace(".", string.Empty);
            GUILayout.Label("m", GetLabelRightAlignStyle(), GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Width limit:", GUILayout.ExpandWidth(false));
            _widthLimit = GUILayout.TextField(_widthLimit, GetTextFieldRightAlignStyle()).Replace(",", string.Empty).Replace(".", string.Empty);
            GUILayout.Label("m", GetLabelRightAlignStyle(), GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Height limit:", GUILayout.ExpandWidth(false));
            _heightLimit = GUILayout.TextField(_heightLimit, GetTextFieldRightAlignStyle()).Replace(",", string.Empty).Replace(".", string.Empty);
            GUILayout.Label("m", GetLabelRightAlignStyle(), GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Show/Hide Resources", GUILayout.ExpandWidth(false)))
            {
                GUIStates.ShowLCResources = !GUIStates.ShowLCResources;
            }
            if (!isModify || activeLC.LCType == LaunchComplexType.Pad)
            {    
                GUILayout.Label(" ");
                _newLCData.isHumanRated = GUILayout.Toggle(_newLCData.isHumanRated, "Human-Rated", GUILayout.ExpandWidth(false));
            }
            GUILayout.EndHorizontal();

            double totalCost = 0;
            if (!isHangar)
            {
                if (curPadCost > oldPadCost)
                    totalCost = curPadCost - oldPadCost;
                else
                    totalCost = (oldPadCost - curPadCost) * 0.5d;
                // Modify case: Additional pads cost less to build, so cost less to modify.
                if (lpMult > 1)
                    totalCost *= 1d + (lpMult - 1d) * Database.SettingsSC.AdditionalPadCostMult;
            }

            double oldTotalCost;
            
            if (isModify)
            {
                // Enforce a min cost for pad size changes
                const double minPadModifyCost = 1000d;
                if (!isHangar && !Mathf.Approximately(activeLC.MassMax, _newLCData.massMax) && totalCost < minPadModifyCost)
                    totalCost = minPadModifyCost;

                double heightAbs = Math.Abs(_newLCData.sizeMax.y - activeLC.SizeMax.y);
                double costScalingFactor = isHangar ? 500d : UtilMath.LerpUnclamped(100d, 1000d, UtilMath.InverseLerp(10d, 55d, UtilMath.Clamp(_newLCData.massOrig, 10d, 50d)));
                double renovateCost = Math.Abs(curVABCost - oldVABCost)
                    + heightAbs * costScalingFactor
                    + Math.Abs(_newLCData.sizeMax.x - activeLC.SizeMax.x) * costScalingFactor * 0.5d
                    + Math.Abs(_newLCData.sizeMax.z - activeLC.SizeMax.z) * costScalingFactor * 0.5d;

                //// moving the roof
                //if (heightAbs > 0.1d)
                //    renovateCost += 3000d;

                if (curVABCost < oldVABCost)
                    renovateCost *= 0.5d;

                if (curVABCost > oldVABCost && renovateCost > curVABCost)
                    renovateCost = curVABCost;

                totalCost += renovateCost + _newLCData.ResModifyCost(activeLC.Stats);
                oldTotalCost = activeLC.Stats.GetCostStats(out _, out _, out _);
            }
            else
            {
                totalCost += curVABCost + curResCost;
                oldTotalCost = 0d;
            }

            if (totalCost > 0)
            {
                double totalCostForMaintenance = curVABCost + curResCost;
                if(!isHangar)
                    totalCostForMaintenance += curPadCost * lpMult;

                // Additional pads cost less
                curPadCost *= Database.SettingsSC.AdditionalPadCostMult;

                GUILayout.BeginHorizontal();
                GUILayout.Label("Max Engineers:", GUILayout.ExpandWidth(false));
                GUILayout.Label($"{LaunchComplex.MaxEngineersCalc(_newLCData.massMax, _newLCData.sizeMax, _newLCData.isHumanRated):N0}", GetLabelRightAlignStyle());
                GUILayout.EndHorizontal();

                GUILayout.Label(" ");

                double buildTime = ConstructionProject.CalculateBuildTime(totalCost, oldTotalCost, SpaceCenterFacility.LaunchPad, null);
                double buildCost = -CurrencyUtils.Funds(TransactionReasonsRP0.StructureConstructionLC, -totalCost);
                string sBuildTime = KSPUtil.PrintDateDelta(buildTime, includeTime: false);
                string costString = isModify ? "Modify Cost:" : "Build Cost:";
                GUILayout.BeginHorizontal();
                GUILayout.Label(costString, GUILayout.ExpandWidth(false));
                GUILayout.Label(new GUIContent($"√{buildCost:N0}", $"Daily: √{(buildCost * 86400d / buildTime):N1}"), GetLabelRightAlignStyle());
                GUILayout.EndHorizontal();
                if (!isModify || _newLCData.lcType == LaunchComplexType.Pad)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(_extraPadsContent, GUILayout.ExpandWidth(false));
                    GUILayout.Label($"√{-CurrencyUtils.Funds(TransactionReasonsRP0.StructureConstructionLC, -curPadCost):N0}", GetLabelRightAlignStyle());
                    GUILayout.EndHorizontal();
                }
                GUILayout.BeginHorizontal();
                GUILayout.Label("Est. construction time:", GUILayout.ExpandWidth(false));
                GUILayout.Label(new GUIContent(sBuildTime, "At 100% work rate"), GetLabelRightAlignStyle());
                GUILayout.EndHorizontal();

                double projectedMaintenance = MaintenanceHandler.Instance.ComputeDailyMaintenanceCost(totalCostForMaintenance, isHangar ? FacilityMaintenanceType.Hangar : FacilityMaintenanceType.LC);

                if (projectedMaintenance > 0d)
                {
                    projectedMaintenance = -CurrencyUtils.Funds(TransactionReasonsRP0.StructureRepairLC, -projectedMaintenance);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Est. Yearly Upkeep:", GUILayout.ExpandWidth(false));
                    GUILayout.Label(new GUIContent($"√{(projectedMaintenance * 365.25d):N0}", $"Daily: √{projectedMaintenance:N1}"), GetLabelRightAlignStyle());
                    GUILayout.EndHorizontal();
                }
            }

            if (!isHangar)
            {
                LCEfficiency closestEff = LCEfficiency.FindClosest(_newLCData, out double closeness);

                GUILayout.BeginHorizontal();
                GUILayout.Label("Closest Existing LC:", GUILayout.ExpandWidth(false));
                GUILayout.Label(closeness > 0 ? closestEff.FirstLCName() : "None", GetLabelRightAlignStyle());
                GUILayout.EndHorizontal();
                    
                GUILayout.BeginHorizontal();
                GUILayout.Label("Commonality:");
                GUILayout.Label(closeness.ToString("P0"), GetLabelRightAlignStyle());
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                if (closeness == 1d)
                {
                    const string sharedEfficTooltip = "Increases in efficiency will be fully shared between these two LCs.";
                    GUILayout.Label(new GUIContent("Uses shared efficiency:", sharedEfficTooltip));
                    GUILayout.Label(new GUIContent(closestEff.Efficiency.ToString("P1"), sharedEfficTooltip), GetLabelRightAlignStyle());
                }
                else
                {
                    const string startingEffTooltip = "Based on current efficiency of the closest LC, if any, and the current tech level. Will be recalculated when this LC becomes operational.";
                    GUILayout.Label(new GUIContent("Starting Efficiency:", startingEffTooltip));
                    GUILayout.Label(new GUIContent((closeness > 0 ? Math.Max(LCEfficiency.MinEfficiency, closestEff.PostClosenessStartingEfficiency(closeness)) : LCEfficiency.MinEfficiency).ToString("P1"), startingEffTooltip), GetLabelRightAlignStyle());
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            _assignEngOnComplete = GUILayout.Toggle(_assignEngOnComplete, new GUIContent((isModify ? "Reassign free engineers on complete" : "Assign free engineers on complete"),
                (isModify ? $"If selected, any unassigned engineers will be reassigned to this facility when reconstruction completes, up to a maximum of {activeLC.Engineers}"
                : "If selected, unassigned engineers will be assigned to this facility when construction completes, up to the facility's maximum capacity")));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(isModify ? "Modify" : "Build") && ValidateLCCreationParameters(_newLCData, isModify ? activeLC : null))
            {
                if (HighLogic.LoadedSceneIsEditor && !SpaceCenterManagement.Instance.EditorVessel.MeetsFacilityRequirements(_newLCData, null))
                {
                    PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new MultiOptionDialog("LCModifyVesselConfirm",
                        "The specified mass, size, and/or supported resources are incompatible with the current vessel in the editor. Are you sure you wish to commence construction?",
                        "Are You Sure?", HighLogic.UISkin,
                    new DialogGUIButton("#autoLOC_190905", () =>
                    {
                        ProcessNewLC(isModify, curPadCost, totalCost, oldTotalCost);
                    }),
                    new DialogGUIButton("#autoLOC_191154", () => { })
                    ), false, HighLogic.UISkin).HideGUIsWhilePopup();
                }
                else
                {
                    ProcessNewLC(isModify, curPadCost, totalCost, oldTotalCost);
                }
            }
            //if (HighLogic.LoadedSceneIsEditor)
            //{
            //    if (GUILayout.Button(new GUIContent("Vessels", "Show/hide the Plans list. Clicking on a vessel there will merge the current LC stats with those needed to support that vessel, if possible.")))
            //    {
            //        _overrideShowBuildPlans = !_overrideShowBuildPlans;
            //    }
            //}
            if (isModify && !isHangar && GUILayout.Button(_cleanButtonContent))
            {
                SetFieldsFromVessel(SpaceCenterManagement.Instance.EditorVessel, activeLC);
            }
            if (isModify && GUILayout.Button(_existingButtonContent))
            {
                SetFieldsFromLC(activeLC);
            }
            if (GUILayout.Button("Cancel"))
            {
                _overrideShowBuildPlans = false;
                _centralWindowPosition.height = 1;
                _centralWindowPosition.width = 150;
                _centralWindowPosition.x = (Screen.width - 150) / 2;
                GUIStates.ShowNewLC = false;
                GUIStates.ShowModifyLC = false;
                GUIStates.ShowLCResources = false;
                if (!HighLogic.LoadedSceneIsEditor || _wasShowBuildList)
                    GUIStates.ShowBuildList = true;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            //CenterWindow(ref _centralWindowPosition);
            if (!Input.GetMouseButtonDown(1) && !Input.GetMouseButtonDown(2))
                GUI.DragWindow();
        }

        private static void ProcessNewLC(bool isModify, double curPadCost, double totalCost, double oldTotalCost)
        {
            LaunchComplex activeLC = SpaceCenterManagement.Instance.ActiveSC.ActiveLC;

            if (isModify && ModifyFailure(out string failedVessels))
            {
                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                         new Vector2(0.5f, 0.5f),
                                         "LCModifyFail",
                                         "Can't Modify",
                                         "The new limits and supported resources for this complex are incompatible with the following vessels:" + failedVessels + "\nEither scrap the vessels in question or choose settings that still support them too.",
                                         KSP.Localization.Localizer.GetStringByTag("#autoLOC_190905"),
                                         true,
                                         HighLogic.UISkin).HideGUIsWhilePopup();
            }
            else
            {

                SpaceCenterManagement.Instance.StarterLCBuilding |= !isModify;

                if (!KSPUtils.CurrentGameIsCareer())
                {
                    RP0Debug.Log($"Building/Modifying launch complex {_newLCData.Name}");
                    if (isModify)
                        activeLC.Modify(_newLCData, Guid.NewGuid());
                    else
                        SpaceCenterManagement.Instance.ActiveSC.LaunchComplexes.Add(new LaunchComplex(_newLCData, SpaceCenterManagement.Instance.ActiveSC));
                }
                else
                {
                    RP0Debug.Log($"Building/Modifying launch complex {_newLCData.Name}");
                    LaunchComplex lc;
                    int engineers = isModify ? activeLC.Engineers : LaunchComplex.MaxEngineersCalc(_newLCData.massMax, _newLCData.sizeMax, _newLCData.isHumanRated);
                    if (isModify)
                    {
                        lc = activeLC;
                        if (SpaceCenterManagement.Instance.staffTarget.LCID == lc.ID)
                            SpaceCenterManagement.Instance.staffTarget.Clear();
                        KCTUtilities.ChangeEngineers(lc, -engineers);
                        SpaceCenterManagement.Instance.ActiveSC.SwitchToPrevLaunchComplex();

                        // We have to update any ongoing pad constructions too
                        foreach (var pc in lc.PadConstructions)
                        {
                            pc.SetBP(curPadCost, 0d);
                            pc.cost = curPadCost;
                        }
                        lc.IsOperational = false;
                        lc.RecalculateBuildRates();
                    }
                    else
                    {
                        lc = new LaunchComplex(_newLCData, SpaceCenterManagement.Instance.ActiveSC);
                        lc.IsOperational = false;
                        SpaceCenterManagement.Instance.ActiveSC.LaunchComplexes.Add(lc);
                    }

                    var modData = new LCData();
                    modData.SetFrom(_newLCData);
                    var lcConstr = new LCConstructionProject
                    {
                        lcID = lc.ID,
                        cost = totalCost,
                        name = _newLCData.Name,
                        isModify = isModify,
                        modId = isModify ? Guid.NewGuid() : lc.ModID,
                        lcData = modData
                    };
                    lcConstr.SetBP(totalCost, oldTotalCost);
                    if (_assignEngOnComplete)
                        lcConstr.engineersToReadd = engineers;
                    SpaceCenterManagement.Instance.ActiveSC.LCConstructions.Add(lcConstr);

                    try
                    {
                        SCMEvents.OnLCConstructionQueued?.Fire(lcConstr, lc);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
                _overrideShowBuildPlans = false;
                GUIStates.ShowNewLC = false;
                GUIStates.ShowLCResources = false;
                GUIStates.ShowModifyLC = false;
                _centralWindowPosition.height = 1;
                _centralWindowPosition.width = 300;
                _centralWindowPosition.x = (Screen.width - 300) / 2;
                if (!HighLogic.LoadedSceneIsEditor || _wasShowBuildList)
                    GUIStates.ShowBuildList = true;

                if (KSPUtils.CurrentGameIsCareer() && isModify)
                {
                    PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                         new Vector2(0.5f, 0.5f),
                                         "LCModifyStart",
                                         "Renovation Begun",
                                         $"All engineers at {_newLCData.Name} have been unassigned. "
                                         + (_assignEngOnComplete ? "They will be reassigned if available when renovation completes."
                                         : $"Remember to reassign engineers to {_newLCData.Name} when it finishes renovation."),
                                         KSP.Localization.Localizer.GetStringByTag("#autoLOC_190905"),
                                         true,
                                         HighLogic.UISkin).HideGUIsWhilePopup();
                }
            }
        }

        public static void DrawLCResourcesWindow(int windowID)
        {
            //LCItem activeLC = KerbalConstructionTimeData.Instance.ActiveKSC.ActiveLaunchComplexInstance;
            //bool isModify = GUIStates.ShowModifyLC;

            if (_resourceCount == 0) GetAllResourceKeys();

            Rect parentPos = _centralWindowPosition;
            _lcResourcesPosition.width = 250;
            _lcResourcesPosition.yMin = parentPos.yMin;
            _lcResourcesPosition.height = parentPos.height;
            _lcResourcesPosition.xMin = parentPos.xMin - _lcResourcesPosition.width;

            float scrollHeight = parentPos.height - 40 - GUI.skin.label.lineHeight * 1;
            _resourceListScroll = GUILayout.BeginScrollView(_resourceListScroll, GUILayout.Width(215), GUILayout.Height(scrollHeight));

            GUILayout.BeginHorizontal();
            GUILayout.Label("Resource");
            GUILayout.Label("Amount", GetLabelRightAlignStyle());
            GUILayout.EndHorizontal();

            for (int i = 0; i < _resourceCount; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(_allResourceKeys[i]);
                _allResourceValues[i] = GUILayout.TextField(_allResourceValues[i], GetTextFieldRightAlignStyle(), GUILayout.Width(90)).Replace(",", string.Empty).Replace(".", string.Empty).Replace("-", string.Empty);

                bool remove = true;
                if (!string.IsNullOrEmpty(_allResourceValues[i]))
                {
                    if (double.TryParse(_allResourceValues[i], out double resValue) && resValue > 0d)
                    {
                        remove = false;
                        _newLCData.resourcesHandled[_allResourceKeys[i]] = Math.Ceiling(resValue);
                    }
                }
                if (remove)
                    _newLCData.resourcesHandled.Remove(_allResourceKeys[i]);


                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();

            //GUILayout.BeginHorizontal();
            //if (isModify)
            //{
            //    if (GUILayout.Button(new GUIContent("Combine with LC", "Combines these resources with complex's existing resource support")))
            //    {
            //        for (int i = 0; i < _resourceCount; i++)
            //        {
            //            if (activeLC.ResourcesHandled.TryGetValue(_allResourceKeys[i], out double oldVal))
            //            {
            //                if (string.IsNullOrEmpty(_allResourceValues[i]) || (double.TryParse(_allResourceValues[i], out double newVal) && newVal < oldVal))
            //                {
            //                    _allResourceValues[i] = Math.Ceiling(oldVal).ToString("F0");
            //                }
            //            }
            //        }
            //    }
            //}
            //else
            //{
            //    GUILayout.Label(string.Empty);
            //}
            //GUILayout.EndHorizontal();

            //GUILayout.BeginHorizontal();
            //if (HighLogic.LoadedSceneIsEditor)
            //{
            //    if (GUILayout.Button(new GUIContent("Add Craft Resources", "Combines these resources with the loaded craft's resources")))
            //    {
            //        for (int i = 0; i < _resourceCount; i++)
            //        {
            //            if (KerbalConstructionTimeData.Instance.EditorVessel.resourceAmounts.TryGetValue(_allResourceKeys[i], out double oldVal))
            //            {
            //                if (string.IsNullOrEmpty(_allResourceValues[i]) || (double.TryParse(_allResourceValues[i], out double newVal) && newVal < oldVal))
            //                {
            //                    _allResourceValues[i] = Math.Ceiling(oldVal).ToString("F0");
            //                }
            //            }
            //        }
            //    }
            //}
            //else
            //{
            //    GUILayout.Label(string.Empty);
            //}
            //GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (HighLogic.LoadedSceneIsEditor)
            {
                GUILayout.Label("");
            }
            else if (GUILayout.Button("Clear Resources"))
            {
                for (int i = 0; i < _resourceCount; i++)
                {
                    _allResourceValues[i] = "0";
                }
            }
            GUILayout.EndHorizontal();
            GUI.DragWindow();
        }

        private static bool ModifyFailure(out string failedVessels)
        {
            failedVessels = string.Empty;
            LaunchComplex activeLC = SpaceCenterManagement.Instance.ActiveSC.ActiveLC;
            foreach (var vp in activeLC.BuildList)
            {
                if (!vp.MeetsFacilityRequirements(_newLCData, null))
                    failedVessels += "\n" + vp.shipName;
            }
            foreach (var vp in activeLC.Warehouse)
            {
                if (!vp.MeetsFacilityRequirements(_newLCData, null))
                    failedVessels += "\n" + vp.shipName;
            }

            return failedVessels != string.Empty;
        }

        private static bool ValidateLCCreationParameters(LCData newLCData, LaunchComplex existingLC)
        {
            if (newLCData.sizeMax == Vector3.zero)
            {
                ScreenMessages.PostScreenMessage("Please enter a valid size");
                return false;
            }

            if (existingLC != null && existingLC.LCType == LaunchComplexType.Hangar)
                return true;

            if (newLCData.GetPadFracLevel() == -1 || newLCData.massMax == 0)
            {
                ScreenMessages.PostScreenMessage("Please enter a valid tonnage limit");
                RP0Debug.Log($"Invalid LC tonnage set, fractional: {newLCData.GetPadFracLevel()}, tonnageLimit {newLCData.massMax}");
                return false;
            }

            if (existingLC != null && !newLCData.IsMassWithinUpAndDowngradeMargins)
            {
                string msg = !newLCData.IsMassWithinUpgradeMargin ? $"Cannot upgrade tonnage above the limit of {newLCData.MaxPossibleMass}t"
                                                                  : $"Cannot downgrade tonnage below the limit of {newLCData.MinPossibleMass}t";
                ScreenMessages.PostScreenMessage(msg);
                RP0Debug.Log($"LC tonnage exceeding upgrade margins, fractional: {newLCData.GetPadFracLevel()}, tonnageLimit {newLCData.massMax}, orig {(existingLC != null ? existingLC.MassOrig : -1f)}");
                return false;
            }

            if (existingLC != null && !existingLC.CanModifyReal)
            {
                ScreenMessages.PostScreenMessage("Please wait for any reconditioning, rollout, rollback, or recovery to complete");
                RP0Debug.Log($"Can't modify LC, recon_rollout in progress");
                return false;
            }

            // Don't bother with name if it's a modify.
            if (existingLC != null)
                return true;

            if (string.IsNullOrEmpty(newLCData.Name))
            {
                ScreenMessages.PostScreenMessage("Enter a name for the new launch complex");
                return false;
            }

            for (int i = 0; i < SpaceCenterManagement.Instance.ActiveSC.LaunchComplexes.Count; i++)
            {
                var lp = SpaceCenterManagement.Instance.ActiveSC.LaunchComplexes[i];
                if (string.Equals(lp.Name, newLCData.Name, StringComparison.OrdinalIgnoreCase))
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
