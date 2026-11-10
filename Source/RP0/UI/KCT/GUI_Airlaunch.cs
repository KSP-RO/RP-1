using System;
using System.Collections.Generic;
using UniLinq;
using UnityEngine;
using FitResult = RP0.AirlaunchTechLevel.FitResult;

namespace RP0
{
    public static partial class KCT_GUI
    {
        private const float CarrierColWidth = 180f;
        private const float MassColWidth = 70f;
        private const float SimAirlaunchWindowWidth = 500f;
        private const float RealAirlaunchWindowWidth = 380f;
        private const float PrepWindowWidth = 720f;
        private const float BuyButtonWidth = 220f;
        private const float RangeColWidth = 110f;
        private const float CostColWidth = 70f;
        private const float TimeColWidth = 90f;

        private static Rect _airlaunchWindowPosition = new Rect(Screen.width / 2 - SimAirlaunchWindowWidth / 2, Screen.height / 2 - 300, SimAirlaunchWindowWidth, 1);
        private static Rect _airlaunchPrepWindowPosition = new Rect(Screen.width / 2 - PrepWindowWidth / 2, Screen.height / 2 - 250, PrepWindowWidth, 1);
        private static string _sKscDistance = "500";
        private static string _sKscAzimuth = "90";
        private static string _sAltitude = "10000";
        private static string _sAzimuth = "270";
        private static string _sVelocity = "180";
        private static string _errorMsg;
        private static AirlaunchParams _airlaunchParams;
        private static string _airlaunchParamsKey;

        /// <summary>
        /// Input state of a carrier grid: the entered release speed/altitude and the selected carrier.
        /// </summary>
        private class CarrierGridState
        {
            public string SelectedCarrier = string.Empty;
            public string VelocityText = string.Empty, AltitudeText = string.Empty;
            public double Velocity, Altitude;
            public bool ParseOk;
            public bool HasFitting;
            public string SpeedRange = string.Empty, AltitudeRange = string.Empty;

            public AirlaunchTechLevel Selected => AirlaunchTechLevel.GetByName(SelectedCarrier);

            /// <summary>
            /// Selects the preferred carrier if it can lift the vessel, otherwise the first one that can,
            /// and fills in that carrier's max speed and altitude.
            /// </summary>
            public void Init(List<AirlaunchTechLevel> fitting, string preferred)
            {
                AirlaunchTechLevel carrier = fitting.FirstOrDefault(c => c.name == preferred) ?? fitting.FirstOrDefault();
                SelectedCarrier = carrier?.name ?? string.Empty;
                SetValues(carrier?.MaxVelocity ?? 0d, carrier?.MaxAltitude ?? 0d);
            }

            public void SetValues(double velocity, double altitude)
            {
                Velocity = velocity;
                Altitude = altitude;
                VelocityText = velocity.ToString("0");
                AltitudeText = altitude.ToString("0");
                ParseOk = true;
            }
        }

        private static readonly CarrierGridState _prepGrid = new CarrierGridState();
        private static readonly CarrierGridState _simGrid = new CarrierGridState();

        private static VesselProject _airlaunchPrepVessel;
        private static readonly Dictionary<string, ReconRolloutProject> _airlaunchPrepProjects = new Dictionary<string, ReconRolloutProject>();

        private static GUIStyle GetOrangeTextStyle()
        {
            if (_orangeText == null)
            {
                _orangeText = new GUIStyle(GUI.skin.label);
                _orangeText.normal.textColor = XKCDColors.Orange;
            }
            return _orangeText;
        }

        /// <summary>
        /// Draws release speed/altitude inputs, the table of carriers that can lift the vessel
        /// and the lists of carriers that can't.
        /// </summary>
        /// <param name="getPrep">Prep project per carrier for cost/time columns, or null to hide those columns (sims)</param>
        private static void DrawCarrierGrid(CarrierGridState s, List<AirlaunchTechLevel> carriers, Func<AirlaunchTechLevel, FitResult> getFit,
                                            Func<AirlaunchTechLevel, ReconRolloutProject> getPrep)
        {
            var fitting = carriers.Where(c => getFit(c) == FitResult.Fits).ToList();
            s.HasFitting = fitting.Count > 0;
            if (fitting.Count == 0)
            {
                GUILayout.Label("None of your carrier aircraft can lift this vessel.", GetLabelStyleYellow());
                s.SelectedCarrier = string.Empty;
            }
            else
            {
                bool velocityOk = double.TryParse(s.VelocityText, out s.Velocity);
                bool altitudeOk = double.TryParse(s.AltitudeText, out s.Altitude);
                s.ParseOk = velocityOk && altitudeOk;

                // Each range only counts carriers that can also reach the other entered value
                var forSpeed = altitudeOk ? fitting.Where(c => s.Altitude >= c.MinAltitude && s.Altitude <= c.MaxAltitude).ToList() : fitting;
                var forAltitude = velocityOk ? fitting.Where(c => s.Velocity >= c.MinVelocity && s.Velocity <= c.MaxVelocity).ToList() : fitting;
                s.SpeedRange = forSpeed.Count > 0 ? $"{forSpeed.Min(c => c.MinVelocity):0} - {forSpeed.Max(c => c.MaxVelocity):0} m/s" : "none at this altitude";
                s.AltitudeRange = forAltitude.Count > 0 ? $"{forAltitude.Min(c => c.MinAltitude):0} - {forAltitude.Max(c => c.MaxAltitude):0} m" : "none at this speed";

                GUILayout.BeginHorizontal();
                GUILayout.Label("Carrier", GUILayout.Width(CarrierColWidth));
                GUILayout.Label("Max mass", GUILayout.Width(MassColWidth));
                GUILayout.Label("Speed", GUILayout.Width(RangeColWidth));
                GUILayout.Label("Altitude", GUILayout.Width(RangeColWidth));
                if (getPrep != null)
                {
                    GUILayout.Label("Prep cost", GUILayout.Width(CostColWidth));
                    GUILayout.Label("Prep time", GUILayout.Width(TimeColWidth));
                }
                GUILayout.EndHorizontal();

                foreach (AirlaunchTechLevel c in fitting)
                {
                    bool inRange = s.ParseOk && c.EnvelopeContains(s.Velocity, s.Altitude);
                    if (!inRange && c.name == s.SelectedCarrier)
                        s.SelectedCarrier = string.Empty;

                    string title = c.title + c.SimAvailabilityTag;
                    bool isSelected = c.name == s.SelectedCarrier;

                    GUILayout.BeginHorizontal();
                    GUI.enabled = inRange;
                    // Toggle drawn as a button: the selected carrier shows as pressed
                    if (GUILayout.Toggle(isSelected, new GUIContent(title, inRange ? null : "Entered speed or altitude is outside this carrier's range"),
                                         GUI.skin.button, GUILayout.Width(CarrierColWidth)) && !isSelected)
                        s.SelectedCarrier = c.name;
                    GUI.enabled = true;
                    GUILayout.Label($"{c.MaxMassTonnes:0.#} t", GUILayout.Width(MassColWidth));
                    GUILayout.Label($"{c.MinVelocity:0} - {c.MaxVelocity:0} m/s", GUILayout.Width(RangeColWidth));
                    GUILayout.Label($"{c.MinAltitude / 1000d:0.#} - {c.MaxAltitude / 1000d:0.#} km", GUILayout.Width(RangeColWidth));
                    if (getPrep != null && getPrep(c) is ReconRolloutProject prep)
                    {
                        GUILayout.Label($"√{-CurrencyUtils.Funds(TransactionReasonsRP0.AirLaunchRollout, -prep.cost):N0}", GUILayout.Width(CostColWidth));
                        GUILayout.Label(RP0DTUtils.GetColonFormattedTime(prep.GetTimeLeft()), GUILayout.Width(TimeColWidth));
                    }
                    GUILayout.EndHorizontal();
                }
            }

            string tooLarge = string.Join(", ", carriers.Where(c => getFit(c) == FitResult.TooLarge).Select(c => c.title));
            string tooHeavy = string.Join(", ", carriers.Where(c => getFit(c) == FitResult.TooHeavy).Select(c => c.title));
            if (tooLarge.Length > 0 || tooHeavy.Length > 0)
                GUILayout.Space(5f);
            if (tooLarge.Length > 0)
                GUILayout.Label($"Vessel too large for: {tooLarge}");
            if (tooHeavy.Length > 0)
                GUILayout.Label($"Vessel too heavy for: {tooHeavy}");
        }

        /// <summary>
        /// Release speed/altitude inputs for a carrier grid, drawn in the same style as the other numeric fields. The available ranges are computed by <see cref="DrawCarrierGrid"/>.
        /// </summary>
        private static void DrawReleaseInputs(CarrierGridState s)
        {
            if (!s.HasFitting)
                return;

            DrawNumericField($"Release speed ({s.SpeedRange}):", ref s.VelocityText, "m/s");
            DrawNumericField($"Release altitude ({s.AltitudeRange}):", ref s.AltitudeText, "m");
            if (!s.ParseOk)
                GUILayout.Label("Enter a numeric speed and altitude", GetOrangeTextStyle());
        }

        #region Capability window (editor)

        private static Rect _airlaunchCapabilityWindowPosition = new Rect(Screen.width / 2 - 250, Screen.height / 2 - 200, 500, 1);
        private static GUIStyle _boldLabelStyle;

        private static GUIStyle GetBoldLabelStyle()
        {
            if (_boldLabelStyle == null)
                _boldLabelStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            return _boldLabelStyle;
        }

        /// <summary>
        /// Lists the unlocked carriers live that can and can't launch the vessel currently in the editor.
        /// </summary>
        public static void DrawAirlaunchCapabilityWindow(int windowID)
        {
            VesselProject vessel = SpaceCenterManagement.Instance.EditorVessel;
            List<AirlaunchTechLevel> carriers = AirlaunchTechLevel.GetAvailable(false);
            var fitting = carriers.Where(c => c.CheckFit(vessel) == FitResult.Fits).ToList();

            GUILayout.BeginVertical();
            Vector3 size = vessel.GetShipSize();
            GUILayout.Label($"Vessel: {vessel.GetTotalMass():0.#} t, {size.x:0.#} x {size.y:0.#} x {size.z:0.#} m");
            GUILayout.Space(5f);

            if (carriers.Count == 0)
            {
                GUILayout.Label("No carrier aircraft unlocked yet.");
            }
            else
            {
                GUILayout.Label("Can launch on:", GetBoldLabelStyle());
                if (fitting.Count == 0)
                {
                    AirlaunchTechLevel needed = AirlaunchTechLevel.FirstFittingInConfig(vessel);
                    GUILayout.Label(needed != null ? $"None. Needs {needed.UnlockHint}" : "None. No carrier aircraft can lift this vessel.",
                                    GetLabelStyleYellow());
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Carrier", GUILayout.Width(CarrierColWidth));
                    GUILayout.Label("Max mass", GUILayout.Width(MassColWidth));
                    GUILayout.Label("Speed", GUILayout.Width(RangeColWidth));
                    GUILayout.Label("Altitude", GUILayout.Width(RangeColWidth));
                    GUILayout.EndHorizontal();
                    foreach (AirlaunchTechLevel c in fitting)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(c.title, GUILayout.Width(CarrierColWidth));
                        GUILayout.Label($"{c.MaxMassTonnes:0.#} t", GUILayout.Width(MassColWidth));
                        GUILayout.Label($"{c.MinVelocity:0} - {c.MaxVelocity:0} m/s", GUILayout.Width(RangeColWidth));
                        GUILayout.Label($"{c.MinAltitude / 1000d:0.#} - {c.MaxAltitude / 1000d:0.#} km", GUILayout.Width(RangeColWidth));
                        GUILayout.EndHorizontal();
                    }
                }

                var notFitting = carriers.Where(c => !fitting.Contains(c)).ToList();
                if (notFitting.Count > 0)
                {
                    GUILayout.Space(5f);
                    GUILayout.Label("Can't launch on:", GetBoldLabelStyle());
                    foreach (AirlaunchTechLevel c in notFitting)
                    {
                        c.CanLaunchVessel(vessel, out string reason);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(c.title, GUILayout.Width(CarrierColWidth));
                        GUILayout.Label(reason);
                        GUILayout.EndHorizontal();
                    }
                }
            }

            GUILayout.Space(5f);
            if (GUILayout.Button("Close"))
                GUIStates.ShowAirlaunchCapability = false;
            GUILayout.EndVertical();

            if (!Input.GetMouseButtonDown(1) && !Input.GetMouseButtonDown(2))
                GUI.DragWindow();
            ClampWindow(ref _airlaunchCapabilityWindowPosition, strict: true);
        }

        #endregion

        #region Prep window

        /// <summary>
        /// Purchase price of a carrier as shown in the prep window: cost after strategies and unlock credit.
        /// </summary>
        private struct CarrierPrice
        {
            public double PreCreditCost, PostCreditCost, Credit;
            public bool CanAfford;
        }

        private static readonly List<AirlaunchTechLevel> _airlaunchPurchasable = new List<AirlaunchTechLevel>();
        private static readonly Dictionary<string, CarrierPrice> _airlaunchPrices = new Dictionary<string, CarrierPrice>();

        public static void OpenAirlaunchPrepWindow(VesselProject b)
        {
            _airlaunchPrepVessel = b;
            RefreshAirlaunchPrepData(b, resetInputs: true);
            _airlaunchPrepWindowPosition.height = 1;
            GUIStates.ShowAirlaunchPrep = true;
        }

        /// <summary>
        /// Rebuilds the per-carrier prep projects and purchase prices. Called on open and after buying a carrier.
        /// </summary>
        private static void RefreshAirlaunchPrepData(VesselProject b, bool resetInputs)
        {
            _airlaunchPrepProjects.Clear();
            string vpID = b.shipID.ToString();
            var fitting = AirlaunchTechLevel.GetAvailable(false).Where(c => c.CheckFit(b) == FitResult.Fits).ToList();
            foreach (AirlaunchTechLevel c in fitting)
                _airlaunchPrepProjects[c.name] = new ReconRolloutProject(b, ReconRolloutProject.RolloutReconType.AirlaunchMount, vpID, carrier: c);

            _airlaunchPurchasable.Clear();
            _airlaunchPrices.Clear();
            foreach (AirlaunchTechLevel c in AirlaunchTechLevel.GetPurchasable().Where(c => c.CheckFit(b) == FitResult.Fits))
            {
                UnlockCreditHandler.Instance.GetPrePostCostAndAffordability(c.PurchaseCost, TransactionReasonsRP0.PartOrUpgradeUnlock,
                    out double pre, out double post, out double credit, out bool canAfford);
                _airlaunchPurchasable.Add(c);
                _airlaunchPrices[c.name] = new CarrierPrice { PreCreditCost = pre, PostCreditCost = post, Credit = credit, CanAfford = canAfford };
            }

            if (resetInputs || string.IsNullOrEmpty(_prepGrid.SelectedCarrier))
                _prepGrid.Init(fitting, _prepGrid.SelectedCarrier);
        }

        private static void CloseAirlaunchPrepWindow()
        {
            GUIStates.ShowAirlaunchPrep = false;
            _airlaunchPrepVessel = null;
            _airlaunchPrepProjects.Clear();
            _airlaunchPurchasable.Clear();
            _airlaunchPrices.Clear();
        }

        /// <summary>
        /// Lists researched carriers that can lift the vessel but haven't been purchased, with a button to buy each.
        /// </summary>
        private static void DrawCarrierPurchaseSection(VesselProject b)
        {
            if (_airlaunchPurchasable.Count == 0)
                return;

            GUILayout.Space(5f);
            GUILayout.Label("Purchase carrier:", GetBoldLabelStyle());
            AirlaunchTechLevel bought = null;
            foreach (AirlaunchTechLevel c in _airlaunchPurchasable)
            {
                CarrierPrice price = _airlaunchPrices[c.name];
                GUILayout.BeginHorizontal();
                GUILayout.Label(c.title, GUILayout.Width(CarrierColWidth));
                GUILayout.Label($"{c.MaxMassTonnes:0.#} t", GUILayout.Width(MassColWidth));
                GUILayout.Label($"{c.MinVelocity:0} - {c.MaxVelocity:0} m/s", GUILayout.Width(RangeColWidth));
                GUILayout.Label($"{c.MinAltitude / 1000d:0.#} - {c.MaxAltitude / 1000d:0.#} km", GUILayout.Width(RangeColWidth));

                string tooltip = price.Credit > 0d ? $"Spending {price.Credit:N0} unlock credit\n(Base cost √{price.PreCreditCost:N0})"
                                                   : $"Base cost √{price.PreCreditCost:N0}";
                if (!price.CanAfford)
                    tooltip = "Not enough funds\n" + tooltip;
                GUI.enabled = price.CanAfford;
                string text;
                if (price.PostCreditCost > 0d && price.Credit > 0d)
                    text = $"Buy √{price.PostCreditCost:N0} + unlock credit";
                else if (price.PostCreditCost > 0d)
                    text = $"Buy √{price.PostCreditCost:N0}";
                else if (price.Credit > 0d)
                    text = $"Buy ({price.Credit:N0} unlock credit)";
                else
                    text = "Unlock (free)";
                if (GUILayout.Button(new GUIContent(text, tooltip), GUILayout.Width(BuyButtonWidth)) && c.Purchase())
                    bought = c;
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            if (bought != null)
            {
                RefreshAirlaunchPrepData(b, resetInputs: false);
                _airlaunchPrepWindowPosition.height = 1;
            }
        }

        public static void DrawAirlaunchPrepWindow(int windowID)
        {
            VesselProject b = _airlaunchPrepVessel;
            if (b == null)
            {
                CloseAirlaunchPrepWindow();
                return;
            }

            GUILayout.BeginVertical();
            GUILayout.Label($"{b.shipName} ({b.GetTotalMass():0.#} t)");

            DrawCarrierGrid(_prepGrid, AirlaunchTechLevel.GetAvailable(false), c => c.CheckFit(b),
                            c => _airlaunchPrepProjects.TryGetValue(c.name, out var p) ? p : null);
            DrawCarrierPurchaseSection(b);

            GUILayout.Space(5f);
            DrawReleaseInputs(_prepGrid);
            GUILayout.Space(5f);
            GUILayout.BeginHorizontal();
            AirlaunchTechLevel carrier = _prepGrid.Selected;
            GUI.enabled = carrier != null && _prepGrid.ParseOk && _airlaunchPrepProjects.ContainsKey(carrier.name);
            if (GUILayout.Button(carrier != null ? $"Prep on {carrier.title}" : "Select a carrier"))
            {
                string vpID = b.shipID.ToString();
                // Re-check in case the vessel got prepped while the window was open
                if (b.LC != null && !b.LC.Recon_Rollout.Exists(r => r.associatedID == vpID))
                {
                    ReconRolloutProject prep = _airlaunchPrepProjects[carrier.name];
                    prep.airlaunchVelocity = _prepGrid.Velocity;
                    prep.airlaunchAltitude = _prepGrid.Altitude;
                    b.LC.Recon_Rollout.Add(prep);
                }
                CloseAirlaunchPrepWindow();
            }
            GUI.enabled = true;
            if (GUILayout.Button("Cancel"))
            {
                CloseAirlaunchPrepWindow();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            if (!Input.GetMouseButtonDown(1) && !Input.GetMouseButtonDown(2))
                GUI.DragWindow();
            ClampWindow(ref _airlaunchPrepWindowPosition, strict: true);
        }

        #endregion

        #region Launch window

        private static void ResetToMax()
        {
            AirlaunchTechLevel lvl = _airlaunchParams.Carrier;
            if (lvl != null)
            {
                _airlaunchParams.KscDistance = lvl.MaxKscDistance;
                _airlaunchParams.Altitude = lvl.MaxAltitude;
                _airlaunchParams.Velocity = lvl.MaxVelocity;
            }
        }

        private static FitResult SimVesselFit(AirlaunchTechLevel carrier)
        {
            SimulationParams simParams = SpaceCenterManagement.Instance.SimulationParams;
            // Sims started before this was tracked have no data; don't block them.
            if (simParams.VesselMass <= 0)
                return FitResult.Fits;
            return carrier.CheckFit(simParams.VesselMass, simParams.VesselSize);
        }

        private static void InitAirlaunchParams()
        {
            _airlaunchParams = new AirlaunchParams(SpaceCenterManagement.Instance.AirlaunchParams);

            if (SpaceCenterManagement.Instance.IsSimulatedFlight)
            {
                var fitting = AirlaunchTechLevel.GetAvailable(true).Where(c => SimVesselFit(c) == FitResult.Fits).ToList();
                _simGrid.Init(fitting, _airlaunchParams.CarrierName);
                _airlaunchParams.CarrierName = _simGrid.SelectedCarrier;
                if (_airlaunchParams.KscDistance == 0)
                    _airlaunchParams.KscDistance = _simGrid.Selected?.MaxKscDistance ?? 0d;
            }
            else
            {
                VesselProject vessel = SpaceCenterManagement.Instance.LaunchedVessel;
                string vpID = vessel.shipID.ToString();
                ReconRolloutProject prep = vessel.LC?.Recon_Rollout.Find(r => r.associatedID == vpID);
                AirlaunchTechLevel carrier = AirlaunchTechLevel.Resolve(prep?.airlaunchCarrier, vessel);
                if (prep != null && carrier != null && string.IsNullOrEmpty(prep.airlaunchCarrier))
                    prep.airlaunchCarrier = carrier.name;   // migrate pre-carrier saves

                _airlaunchParams.CarrierName = carrier?.name ?? string.Empty;
                ResetToMax();
                // Use the speed and altitude chosen at prep time
                if (prep != null && prep.airlaunchVelocity > 0)
                {
                    _airlaunchParams.Velocity = prep.airlaunchVelocity;
                    _airlaunchParams.Altitude = prep.airlaunchAltitude;
                }
                if (SpaceCenterManagement.Instance.AirlaunchParams.KscDistance > 0)
                    _airlaunchParams.KscDistance = SpaceCenterManagement.Instance.AirlaunchParams.KscDistance;
            }

            SetAirlaunchStrings(true);
        }

        private static void SetAirlaunchStrings(bool all)
        {
            _sKscDistance = (_airlaunchParams.KscDistance / 1000).ToString();
            _sAltitude = _airlaunchParams.Altitude.ToString();
            _sVelocity = _airlaunchParams.Velocity.ToString();
            if (all)
            {
                _sAzimuth = _airlaunchParams.LaunchAzimuth.ToString();
                _sKscAzimuth = _airlaunchParams.KscAzimuth.ToString();
            }
        }

        private static void DrawNumericField(string label, ref string text, string unit)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.ExpandWidth(true));
            text = GUILayout.TextField(text, GUILayout.Width(70f));
            GUILayout.Label(unit, GUILayout.Width(30f));
            GUILayout.EndHorizontal();
        }

        public static void DrawAirlaunchWindow(int windowID)
        {
            bool isSim = SpaceCenterManagement.Instance.IsSimulatedFlight;
            // Re-initialize when switching between sim and real launches or between vessels, so a stale carrier isn't reused.
            string paramsKey = isSim ? "sim" : SpaceCenterManagement.Instance.LaunchedVessel.shipID.ToString();
            if (_airlaunchParams == null || _airlaunchParamsKey != paramsKey)
            {
                _airlaunchParamsKey = paramsKey;
                InitAirlaunchParams();
                _airlaunchWindowPosition.width = isSim ? SimAirlaunchWindowWidth : RealAirlaunchWindowWidth;
                _airlaunchWindowPosition.height = 1;
            }

            GUILayout.BeginVertical();

            if (isSim)
            {
                // Sims have no prep step: pick the carrier, release speed and altitude here
                DrawCarrierGrid(_simGrid, AirlaunchTechLevel.GetAvailable(true), SimVesselFit, null);
                GUILayout.Space(5f);
            }
            else
            {
                GUILayout.Label($"Carrier aircraft: {_airlaunchParams.Carrier?.title ?? "none"}");
            }

            DrawNumericField("Distance from Space Center:", ref _sKscDistance, "km");
            DrawNumericField("Azimuth from Space Center:", ref _sKscAzimuth, "°");
            DrawNumericField("Launch azimuth:", ref _sAzimuth, "°");
            if (isSim)
            {
                DrawReleaseInputs(_simGrid);
            }
            else
            {
                AirlaunchTechLevel carrier = _airlaunchParams.Carrier;
                DrawNumericField(carrier != null ? $"Release speed ({carrier.MinVelocity:0} - {carrier.MaxVelocity:0} m/s):" : "Release speed:", ref _sVelocity, "m/s");
                DrawNumericField(carrier != null ? $"Release altitude ({carrier.MinAltitude:0} - {carrier.MaxAltitude:0} m):" : "Release altitude:", ref _sAltitude, "m");
            }

            try
            {
                _airlaunchParams.LaunchAzimuth = double.Parse(_sAzimuth);
                _airlaunchParams.KscDistance = double.Parse(_sKscDistance) * 1000;
                _airlaunchParams.KscAzimuth = double.Parse(_sKscAzimuth);
                if (isSim)
                {
                    _airlaunchParams.CarrierName = _simGrid.SelectedCarrier;
                    _airlaunchParams.Velocity = _simGrid.Velocity;
                    _airlaunchParams.Altitude = _simGrid.Altitude;
                }
                else
                {
                    _airlaunchParams.Altitude = double.Parse(_sAltitude);
                    _airlaunchParams.Velocity = double.Parse(_sVelocity);
                }

                if (isSim && !_simGrid.ParseOk)
                    _errorMsg = "Parsing error";
                else
                    _airlaunchParams.Validate(out _errorMsg);
            }
            catch
            {
                _errorMsg = "Parsing error";
            }

            GUILayout.Label(_errorMsg ?? string.Empty, GetOrangeTextStyle());

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Launch"))
            {
                HandleLaunch();
            }
            if (!isSim && GUILayout.Button("Max"))
            {
                ResetToMax();
                SetAirlaunchStrings(false);
            }
            if (GUILayout.Button("Cancel"))
            {
                _centralWindowPosition.width = 150;
                _centralWindowPosition.x = (Screen.width - 150) / 2;
                GUIStates.ShowAirlaunch = false;
                GUIStates.ShowBuildList = true;
                _errorMsg = null;
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            if (!Input.GetMouseButtonDown(1) && !Input.GetMouseButtonDown(2))
                GUI.DragWindow();
            ClampWindow(ref _airlaunchWindowPosition, strict: true);
        }

        private static void HandleLaunch()
        {
            try
            {
                bool isSim = SpaceCenterManagement.Instance.IsSimulatedFlight;
                _airlaunchParams.KCTVesselId = isSim ? FlightGlobals.ActiveVessel.id : SpaceCenterManagement.Instance.LaunchedVessel.shipID;

                bool valid = _airlaunchParams.Validate(out _errorMsg);
                // Sims have no prep step, so check the payload against the carrier here
                if (valid && isSim && SimVesselFit(_airlaunchParams.Carrier) != FitResult.Fits)
                {
                    _errorMsg = "Carrier cannot lift this vessel";
                    valid = false;
                }
                if (valid)
                {
                    _errorMsg = null;
                    if (isSim)
                    {
                        var kct = SpaceCenterManagement.Instance;
                        kct.StartCoroutine(AirlaunchUtils.DoAirlaunchRoutine(_airlaunchParams, _airlaunchParams.KCTVesselId, skipCountdown: true));
                        ToggleVisibility(false);
                        return;
                    }

                    SpaceCenterManagement.Instance.AirlaunchParams = _airlaunchParams;
                    _airlaunchParams = null;

                    VesselProject b = SpaceCenterManagement.Instance.LaunchedVessel;
                    if (!b.IsCrewable())
                    {
                        b.Launch();
                    }
                    else
                    {
                        GUIStates.ShowAirlaunch = false;
                        SpaceCenterManagement.ToolbarControl?.SetFalse();
                        _centralWindowPosition.height = 1;
                        AssignInitialCrew();
                        GUIStates.ShowShipRoster = true;
                    }
                }
            }
            catch (Exception ex)
            {
                _errorMsg = ex.Message;
                Debug.LogException(ex);
            }
        }

        #endregion
    }
}
