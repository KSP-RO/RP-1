using System;
using System.Collections.Generic;
using UnityEngine;
using static RP0.UIBase;
using RP0;

namespace KerbalConstructionTime
{
    public static partial class KCT_GUI
    {
        private static string _tonnageLimit = "1";
        private static string _heightLimit = "10";
        private static string _widthLimit = "2";
        private static string _lengthLimit = "2";
        private static LCItem.LCData _newLCData = new LCItem.LCData();
        private static int _resourceCount = 0;
        private static List<string> _allResourceKeys = new List<string>();
        private static List<string> _allResourceValues = new List<string>();
        private static Vector2 _resourceListScroll = new Vector2();
        private static bool _overrideShowBuildPlans = false;
        private static float _requiredTonnage = 0;

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
                    _allResourceValues[i] = resourceValue.ToString("F0");
                }
                else
                {
                    _allResourceValues[i] = "0";
                }
            }
        }

        private static void SetFieldsFromStartingLCData(LCItem.LCData old)
        {
            _newLCData.SetFrom(old);
            _newLCData.Name = _newName = $"Launch Complex {(KCTGameStates.ActiveKSC.LaunchComplexes.Count)}";
            SetStrings();
            SetResources();
        }

        private static void SetFieldsFromLC(LCItem LC)
        {
            _newLCData.SetFrom(LC);
            SetStrings();
            SetResources();
        }

        private static void SetFieldsFromVessel(BuildListVessel blv, LCItem lc = null)
        {
            _newLCData.sizeMax.z = Mathf.Ceil(blv.ShipSize.z * 1.2f);
            _newLCData.sizeMax.x = Mathf.Ceil(blv.ShipSize.x * 1.2f);
            _newLCData.sizeMax.y = Mathf.Ceil(blv.ShipSize.y * 1.1f);
            if (lc == null)
            {
                _newLCData.massMax = Mathf.Ceil(blv.mass * 1.1f);
                if (blv.mass < 1f) // special case
                    _newLCData.massMax = 1f;

                _newLCData.isHumanRated = blv.humanRated;

                _newLCData.Name = _newName = $"Launch Complex {(KCTGameStates.ActiveKSC.LaunchComplexes.Count)}";
                _newLCData.massOrig = _newLCData.massMax;
            }
            else
            {
                _newLCData.Name = _newName = lc.Name;
                if (lc.LCType == LaunchComplexType.Pad)
                {
                    _newLCData.massOrig = lc.MassOrig;
                    _newLCData.massMax = Mathf.Ceil(blv.mass * 1.1f);

                    if (_newLCData.massMax > _newLCData.MaxPossibleMass)
                    {
                        if (blv.mass <= _newLCData.MaxPossibleMass)
                            _newLCData.massMax = _newLCData.MaxPossibleMass;
                    }
                    else if (_newLCData.massMax < _newLCData.MinPossibleMass)
                    {
                        _newLCData.massMax = lc.MassMax;
                        if (blv.mass < _newLCData.MassMin)
                        {
                            _newLCData.massMax = _newLCData.MinPossibleMass;
                            if (blv.mass < _newLCData.MassMin)
                            {
                                // oh well. Set what we want, and let the user see the validate fail.
                                _newLCData.massMax = Mathf.Ceil((float)(blv.mass * 1.1d));
                            }
                        }
                    }
                    if (blv.mass < 1f) // special case
                        _newLCData.massMax = 1f;
                }
            }

            _newLCData.resourcesHandled.Clear();
            foreach (var kvp in blv.resourceAmounts)
            {
                if (kvp.Value * PartResourceLibrary.Instance.GetDefinition(kvp.Key).density > blv.GetTotalMass() * Formula.ResourceValidationRatioOfVesselMassMin)
                    _newLCData.resourcesHandled.Add(kvp.Key, Math.Max(_MinResourceVolume, kvp.Value * 1.1d));
            }
            SetStrings();
            SetResources();
        }

        private static void SetFieldsFromVesselKeepOld(BuildListVessel blv, LCItem lc)
        {
            if (lc == null)
            {
                if (blv.mass < _newLCData.MassMin && LCItem.LCData.CalcMassMaxFromMin(blv.mass) < _requiredTonnage)
                    return;
            }
            else
            {
                SetFieldsFromLC(lc);
            }

            if(_newLCData.sizeMax.z < blv.ShipSize.z)
                _newLCData.sizeMax.z = Mathf.Ceil(blv.ShipSize.z * 1.1f);
            if (_newLCData.sizeMax.x < blv.ShipSize.x)
                _newLCData.sizeMax.x = Mathf.Ceil(blv.ShipSize.x * 1.1f);
            if (_newLCData.sizeMax.y < blv.ShipSize.y)
                _newLCData.sizeMax.y = Mathf.Ceil(blv.ShipSize.y * 1.1f);

            if (_newLCData.massMax < blv.mass)
            {
                float desiredMass = Mathf.Ceil(blv.mass * 1.1f);
                _newLCData.massMax = Math.Min(desiredMass, _newLCData.MaxPossibleMass);
                // If we still fail, just set it.
                if (_newLCData.massMax < blv.mass)
                    _newLCData.massMax = desiredMass;
            }
            else if (_newLCData.MassMin > blv.mass)
            {
                _newLCData.massMax = Math.Max(_newLCData.MinPossibleMass, LCItem.LCData.CalcMassMaxFromMin(blv.mass * 0.9f));
                if (blv.mass < _newLCData.MassMin)
                {
                    _newLCData.massMax = _newLCData.MinPossibleMass;
                    if (blv.mass < _newLCData.MassMin)
                    {
                        // oh well. Set what we want, and let the user see the validate fail.
                        _newLCData.massMax = Mathf.Ceil((float)(blv.mass * 1.1d));
                    }
                }
            }

            if (lc != null)
                _requiredTonnage = _newLCData.massMax;

            _newLCData.isHumanRated |= blv.humanRated;
            foreach (var kvp in blv.resourceAmounts)
            {
                if (kvp.Value * PartResourceLibrary.Instance.GetDefinition(kvp.Key).density > blv.GetTotalMass() * Formula.ResourceValidationRatioOfVesselMassMin)
                {
                    _newLCData.resourcesHandled.TryGetValue(kvp.Key, out double oldAmount);
                    if (oldAmount < kvp.Value)
                        _newLCData.resourcesHandled[kvp.Key] = Math.Max(_MinResourceVolume, kvp.Value * 1.1d);
                }
            }
            // Reset based on our new values.
            SetStrings();
            SetResources();
        }

        private static void GetAllResourceKeys()
        {
            foreach (var res in GuiDataAndWhitelistItemsDatabase.ValidFuelRes)
            {
                if (!GuiDataAndWhitelistItemsDatabase.PadIgnoreRes.Contains(res))
                {
                    _allResourceKeys.Add(res);
                    _allResourceValues.Add(string.Empty);
                    ++_resourceCount;
                }
            }
        }

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

                activeLC.Stats.GetCostStats(out oldPadCost, out oldVABCost, out _); // we'll compute resource cost delta ourselves
                lpMult = activeLC.LaunchPadCount; // i.e. skip pads that are building, since we'll change their PadConstructions
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Name:", GUILayout.ExpandWidth(false));
                _newLCData.Name = GUILayout.TextField(_newLCData.Name);
                GUILayout.EndHorizontal();
            }

            GUILayout.Label(isModify ? "New Limits" : "Launch Complex Limits:");

            bool isHangar = isModify && activeLC.LCType == LaunchComplexType.Hangar;
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
                GUILayout.BeginHorizontal();
                GUILayout.Label("Minimum tonnage:");
                GUILayout.Label(minTonnage.ToString("N0"), GetLabelRightAlignStyle(), GUILayout.ExpandWidth(false));
                GUILayout.EndHorizontal();

                if (isModify)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Upgrade Limit for max tng:");
                    GUILayout.Label($"{(int)(_newLCData.massOrig * 2f):N0}", GetLabelRightAlignStyle(), GUILayout.ExpandWidth(false));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Downgrade Limit for max tng:");
                    GUILayout.Label($"{Math.Max(1, (int)(_newLCData.massOrig * 0.5f)):N0}", GetLabelRightAlignStyle(), GUILayout.ExpandWidth(false));
                    GUILayout.EndHorizontal();

                    
                }
                else
                {
                    _newLCData.massOrig = _newLCData.massMax;
                    if (_newLCData.massOrig < 1.5f)
                        _newLCData.massOrig = 1.5f;

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Upgrade Limit for max tng:");
                    GUILayout.Label($"{Math.Max(3, _newLCData.massOrig * 2):N0}", GetLabelRightAlignStyle(), GUILayout.ExpandWidth(false));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Downgrade Limit for max tng:");
                    GUILayout.Label($"{Math.Max(1, _newLCData.massOrig / 2):N0}", GetLabelRightAlignStyle(), GUILayout.ExpandWidth(false));
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
                    totalCost *= 1d + (lpMult - 1d) * PresetManager.Instance.ActivePreset.GeneralSettings.AdditionalPadCostMult;
            }

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
            }
            else
            {
                totalCost += curVABCost + curResCost;
            }

            if (totalCost > 0)
            {
                double totalCostForMaintenance = curVABCost + curResCost;
                if(!isHangar)
                    totalCostForMaintenance += curPadCost * lpMult;

                // Additional pads cost less
                curPadCost *= PresetManager.Instance.ActivePreset.GeneralSettings.AdditionalPadCostMult;

                GUILayout.BeginHorizontal();
                GUILayout.Label("Max Engineers:", GUILayout.ExpandWidth(false));
                GUILayout.Label($"{LCItem.MaxEngineersCalc(_newLCData.massMax, _newLCData.sizeMax, _newLCData.isHumanRated):N0}", GetLabelRightAlignStyle());
                GUILayout.EndHorizontal();

                GUILayout.Label(" ");

                double buildTime = ConstructionBuildItem.CalculateBuildTime(totalCost, SpaceCenterFacility.LaunchPad, null);
                string sBuildTime = KSPUtil.PrintDateDelta(buildTime, includeTime: false);
                string costString = isModify ? "Renovate Cost:" : "Build Cost:";
                GUILayout.BeginHorizontal();
                GUILayout.Label(costString, GUILayout.ExpandWidth(false));
                GUILayout.Label($"√{-RP0.CurrencyUtils.Funds(RP0.TransactionReasonsRP0.StructureConstructionLC, -totalCost):N0}", GetLabelRightAlignStyle());
                GUILayout.EndHorizontal();
                if (!isModify || _newLCData.lcType == LaunchComplexType.Pad)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Extra Pad Cost:", GUILayout.ExpandWidth(false));
                    GUILayout.Label($"√{-RP0.CurrencyUtils.Funds(RP0.TransactionReasonsRP0.StructureConstructionLC, -curPadCost):N0}", GetLabelRightAlignStyle());
                    GUILayout.EndHorizontal();
                }
                GUILayout.BeginHorizontal();
                GUILayout.Label("Est. construction time:", GUILayout.ExpandWidth(false));
                GUILayout.Label(new GUIContent(sBuildTime, "At 100% work rate"), GetLabelRightAlignStyle());
                GUILayout.EndHorizontal();

                double projectedMaintenance = RP0.MaintenanceHandler.Instance.ComputeDailyMaintenanceCost(totalCostForMaintenance, isHangar ? RP0.FacilityMaintenanceType.Hangar : RP0.FacilityMaintenanceType.LC);

                if (projectedMaintenance > 0d)
                {
                    projectedMaintenance = -RP0.CurrencyUtils.Funds(RP0.TransactionReasonsRP0.StructureRepairLC, -projectedMaintenance);
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
            if (GUILayout.Button(isModify ? "Renovate" : "Build") && ValidateLCCreationParameters(_newLCData.Name, _newLCData.GetPadFracLevel(), _newLCData.massMax, _newLCData.sizeMax, isModify ? activeLC : null))
            {
                if (isModify && ModifyFailure(out string failedVessels))
                {
                    BackupUIState();
                    GUIStates.ShowModifyLC = false;
                    GUIStates.ShowLCResources = false;
                    PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                             new Vector2(0.5f, 0.5f),
                                             "LCModifyFail",
                                             "Can't Modify",
                                             "The new limits and supported resources for this complex are incompatible with the following vessels:" + failedVessels + "\nEither scrap the vessels in question or choose settings that still support them too.",
                                             KSP.Localization.Localizer.GetStringByTag("#autoLOC_190905"),
                                             true,
                                             HighLogic.UISkin).PrePostActions(onDestroyAction: RestorePrevUIState);
                }
                else
                {

                    KCTGameStates.StarterLCBuilding |= !isModify;

                    if (!Utilities.CurrentGameIsCareer())
                    {
                        KCTDebug.Log($"Building/Modifying launch complex {_newLCData.Name}");
                        if (isModify)
                            activeLC.Modify(_newLCData, Guid.NewGuid());
                        else
                            KCTGameStates.ActiveKSC.LaunchComplexes.Add(new LCItem(_newLCData, KCTGameStates.ActiveKSC));
                    }
                    else
                    {
                        KCTDebug.Log($"Building/Modifying launch complex {_newLCData.Name}");
                        LCItem lc;
                        if (isModify)
                        {
                            lc = activeLC;
                            Utilities.ChangeEngineers(lc, -lc.Engineers);
                            KCTGameStates.ActiveKSC.SwitchToPrevLaunchComplex();
                            
                            // We have to update any ongoing pad constructions too
                            foreach (var pc in lc.PadConstructions)
                            {
                                pc.SetBP(curPadCost);
                                pc.cost = curPadCost;
                            }
                            lc.IsOperational = false;
                            lc.RecalculateBuildRates();
                        }
                        else
                        {
                            lc = new LCItem(_newLCData, KCTGameStates.ActiveKSC);
                            lc.IsOperational = false;
                            KCTGameStates.ActiveKSC.LaunchComplexes.Add(lc);
                        }

                        var modData = new LCItem.LCData();
                        modData.SetFrom(_newLCData);
                        var lcConstr = new LCConstruction
                        {
                            lcID = lc.ID,
                            cost = totalCost,
                            name = _newLCData.Name,
                            isModify = isModify,
                            modId = isModify ? Guid.NewGuid() : lc.ModID,
                            lcData = modData
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
                    _overrideShowBuildPlans = false;
                    GUIStates.ShowNewLC = false;
                    GUIStates.ShowLCResources = false;
                    GUIStates.ShowModifyLC = false;
                    _centralWindowPosition.height = 1;
                    _centralWindowPosition.width = 300;
                    _centralWindowPosition.x = (Screen.width - 300) / 2;
                    if (!HighLogic.LoadedSceneIsEditor || _wasShowBuildList)
                        GUIStates.ShowBuildList = true;
                }
            }
            if (HighLogic.LoadedSceneIsEditor)
            {
                if (GUILayout.Button(new GUIContent("Vessels", "Show/hide the Plans list. Clicking on a vessel there will merge the current LC stats with those needed to support that vessel, if possible.")))
                {
                    _overrideShowBuildPlans = !_overrideShowBuildPlans;
                }
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
            CenterWindow(ref _centralWindowPosition);
        }

        public static void DrawLCResourcesWindow(int windowID)
        {
            //LCItem activeLC = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance;
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
                if (_newLCData.lcType == LaunchComplexType.Hangar && GuiDataAndWhitelistItemsDatabase.HangarIgnoreRes.Contains(_allResourceKeys[i]))
                    continue;

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
            //            if (KCTGameStates.EditorVessel.resourceAmounts.TryGetValue(_allResourceKeys[i], out double oldVal))
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
        }

        private static bool ModifyFailure(out string failedVessels)
        {
            failedVessels = string.Empty;
            LCItem activeLC = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance;
            foreach (var blv in activeLC.BuildList)
            {
                if (!blv.MeetsFacilityRequirements(_newLCData, null))
                    failedVessels += "\n" + blv.shipName;
            }
            foreach (var blv in activeLC.Warehouse)
            {
                if (!blv.MeetsFacilityRequirements(_newLCData, null))
                    failedVessels += "\n" + blv.shipName;
            }

            return failedVessels != string.Empty;
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
                Debug.Log($"[RP-0] Invalid LC tonnage set, fractional: {fractionalPadLvl}, tonnageLimit {tonnageLimit}, orig {(lc != null ? lc.MassOrig : -1f)}");
                return false;
            }

            // Don't bother with name if it's a modify.
            if (lc != null)
                return true;

            if (string.IsNullOrEmpty(newName))
            {
                ScreenMessages.PostScreenMessage("Enter a name for the new launch complex");
                return false;
            }

            for (int i = 0; i < KCTGameStates.ActiveKSC.LaunchComplexes.Count; i++)
            {
                var lp = KCTGameStates.ActiveKSC.LaunchComplexes[i];
                if (string.Equals(lp.Name, newName, StringComparison.OrdinalIgnoreCase))
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
