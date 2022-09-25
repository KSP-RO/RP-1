using UnityEngine;
using KSP.UI.Screens;
using UnityEngine.UI;
using System.Collections.Generic;

namespace KerbalConstructionTime
{
    [KSPAddon(KSPAddon.Startup.TrackingStation, false)]
    public class TrackingStationAddon : KerbalConstructionTime
    {
        private Button.ButtonClickedEvent _originalCallback, _flyCallback;

        public new void Start()
        {
            base.Start();
            if (KCT_GUI.IsPrimarilyDisabled)
                return;

            KCTDebug.Log("KCT_TS, Start");
            if (FindObjectOfType<SpaceTracking>() is SpaceTracking trackingStation)
            {
                _originalCallback = trackingStation.RecoverButton.onClick;
                _flyCallback = trackingStation.FlyButton.onClick;

                trackingStation.RecoverButton.onClick = new Button.ButtonClickedEvent();
                trackingStation.RecoverButton.onClick.AddListener(NewRecoveryFunctionTrackingStation);
            }
        }

        private void Fly()
        {
            _flyCallback.Invoke();
        }

        private void KCT_Recovery()
        {
            DialogGUIBase[] options = new DialogGUIBase[2];
            options[0] = new DialogGUIButton("Go to Flight scene", Fly);
            options[1] = new DialogGUIButton("Cancel", () => { });

            var diag = new MultiOptionDialog("scrapVesselPopup", "KCT can only recover vessels in the Flight scene", "Recover Vessel", null, options: options);
            PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), diag, false, HighLogic.UISkin);
        }

        public void RecoverToVAB()
        {
            KCT_Recovery();
        }

        public void RecoverToSPH()
        {
            KCT_Recovery();
        }

        public void DoNormalRecovery()
        {
            _originalCallback.Invoke();
        }

        public void NewRecoveryFunctionTrackingStation()
        {
            if (!(FindObjectOfType(typeof(SpaceTracking)) is SpaceTracking ts
                && ts.SelectedVessel is Vessel selectedVessel))
            {
                Debug.LogError("[KCT] No Vessel selected.");
                return;
            }

            var options = new List<DialogGUIBase>();
            if (Utilities.IsSphRecoveryAvailable(selectedVessel))
                options.Add(new DialogGUIButton("Recover to SPH", RecoverToSPH));
            if (Utilities.IsVabRecoveryAvailable(selectedVessel))
                options.Add(new DialogGUIButton("Recover to VAB", RecoverToVAB));
            options.Add(new DialogGUIButton("Normal recovery", DoNormalRecovery));
            options.Add(new DialogGUIButton("Cancel", () => { }));

            var diag = new MultiOptionDialog("scrapVesselPopup", "Do you want KCT to do the recovery?", "Recover Vessel", null, options: options.ToArray());
            PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), diag, false, HighLogic.UISkin);
        }
    }
}
