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
                if (isSPHAllowed)
                    options.Add(new DialogGUIButton("Recover to SPH", RecoverToSPH));
                if (isVABAllowed)
                    options.Add(new DialogGUIButton("Recover to VAB", RecoverToVAB));
                options.Add(new DialogGUIButton("Normal recovery", DoNormalRecovery));
            }
            else
                options.Add(new DialogGUIButton("Recover", DoNormalRecovery));
            options.Add(new DialogGUIButton("Cancel", () => { }));

            var diag = new MultiOptionDialog("RecoverVesselPopup",
                "Do you want KCT to do the recovery?", 
                "RP-1's KCT", 
                null, options: options.ToArray());
            PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), diag, false, HighLogic.UISkin);
        }
    }
}
