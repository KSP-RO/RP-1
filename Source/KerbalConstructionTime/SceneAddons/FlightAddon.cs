using UnityEngine;
using KSP.UI.Screens;
using UnityEngine.UI;

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
            var altimeter = FindObjectOfType<AltimeterSliderButtons>();
            if (altimeter != null)
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
            bool isSPH = FlightGlobals.ActiveVessel != null && FlightGlobals.ActiveVessel.IsRecoverable && 
                         FlightGlobals.ActiveVessel.IsClearToSave() == ClearToSaveStatus.CLEAR;
            bool isVAB = Utilities.IsVabRecoveryAvailable();

            int cnt = 2;
            if (!FlightGlobals.ActiveVessel.isEVA)
            {
                if (isSPH) cnt++;
                if (isVAB) cnt++;
            }
            DialogGUIBase[] options = new DialogGUIBase[cnt];
            cnt = 0;
            if (!FlightGlobals.ActiveVessel.isEVA)
            {
                if (isSPH)
                {
                    options[cnt++] = new DialogGUIButton("Recover to SPH", RecoverToSPH);
                }
                if (isVAB)
                {
                    options[cnt++] = new DialogGUIButton("Recover to VAB", RecoverToVAB);
                }
                options[cnt++] = new DialogGUIButton("Normal recovery", DoNormalRecovery);
            } 
            else
                options[cnt++] = new DialogGUIButton("Recover", DoNormalRecovery);
            options[cnt] = new DialogGUIButton("Cancel", () => { });

            var diag = new MultiOptionDialog("RecoverVesselPopup", 
                "Do you want KCT to do the recovery?", 
                "RP-1's (KCT)", 
                null, options: options);
            PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), diag, false, HighLogic.UISkin);
        }
    }
}
