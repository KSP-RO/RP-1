using UnityEngine;
using KSP.UI.Screens;
using UnityEngine.UI;
using System.Collections.Generic;

namespace KerbalConstructionTime
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class FlightAddon : KerbalConstructionTime
    {
        private Button.ButtonClickedEvent _originalCallback;

        public new void Start()
        {
            base.Start();
            if (KCT_GUI.IsPrimarilyDisabled)
                return;
            KCTDebug.Log("KCT_Flight, Start");
            if (FindObjectOfType<AltimeterSliderButtons>() is AltimeterSliderButtons altimeter)
            {
                _originalCallback = altimeter.vesselRecoveryButton.onClick;

                altimeter.vesselRecoveryButton.onClick = new Button.ButtonClickedEvent();
                altimeter.vesselRecoveryButton.onClick.AddListener(RecoverVessel);
            }
        }

        public void RecoverToVAB()
        {
            if (!Utilities.RecoverActiveVesselToStorage(BuildListVessel.ListType.VAB))
            {
                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "vesselRecoverErrorPopup", "Error!", "There was an error while recovering the ship. Sometimes reloading the scene and trying again works. Sometimes a vessel just can't be recovered this way and you must use the stock recover system.", "OK", false, HighLogic.UISkin);
            }
        }

        public void RecoverToSPH()
        {
            if (!Utilities.RecoverActiveVesselToStorage(BuildListVessel.ListType.SPH))
            {
                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "recoverShipErrorPopup", "Error!", "There was an error while recovering the ship. Sometimes reloading the scene and trying again works. Sometimes a vessel just can't be recovered this way and you must use the stock recover system.", "OK", false, HighLogic.UISkin);
            }
        }

        public void DoNormalRecovery()
        {
            _originalCallback.Invoke();
        }

        public void RecoverVessel()
        {
            if (KCTGameStates.IsSimulatedFlight)
            {
                KCT_GUI.GUIStates.ShowSimulationGUI = true;
                return;
            }

            bool isSPHAllowed = Utilities.IsSphRecoveryAvailable(FlightGlobals.ActiveVessel);
            bool isVABAllowed = Utilities.IsVabRecoveryAvailable(FlightGlobals.ActiveVessel);
            var options = new List<DialogGUIBase>();
            if (!FlightGlobals.ActiveVessel.isEVA)
            {
                string nodeTitle = ResearchAndDevelopment.GetTechnologyTitle(PresetManager.Instance.ActivePreset.GeneralSettings.VABRecoveryTech);
                string techLimitText = string.IsNullOrEmpty(nodeTitle) ? string.Empty :
                                       $"\nAdditionally requires {nodeTitle} tech node to be researched (unless the vessel is in Prelaunch state).";
                string genericReuseText = "Allows the vessel to be launched again after a short recovery delay.";

                options.Add(new DialogGUIButtonWithTooltip("Recover to SPH", RecoverToSPH)
                {
                    OptionInteractableCondition = () => isSPHAllowed,
                    tooltipText = isSPHAllowed ? genericReuseText : "Can only be used when the vessel was built in SPH."
                });

                options.Add(new DialogGUIButtonWithTooltip("Recover to VAB", RecoverToVAB)
                {
                    OptionInteractableCondition = () => isVABAllowed,
                    tooltipText = isVABAllowed ? genericReuseText : $"Can only be used when the vessel was built in VAB.{techLimitText}"
                });

                options.Add(new DialogGUIButtonWithTooltip("Normal recovery", DoNormalRecovery)
                {
                    tooltipText = "Vessel will be scrapped and the total value of recovered parts will be refunded."
                });
            }
            else
            {
                options.Add(new DialogGUIButtonWithTooltip("Recover", DoNormalRecovery));
            }

            options.Add(new DialogGUIButton("Cancel", () => { }));

            var diag = new MultiOptionDialog("RecoverVesselPopup",
                string.Empty, 
                "Recover vessel", 
                null, options: options.ToArray());
            PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), diag, false, HighLogic.UISkin);
        }
    }
}
