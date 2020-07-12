using UnityEngine;
using KSP.UI.Screens;
using UnityEngine.UI;

namespace KerbalConstructionTime
{
    [KSPAddon(KSPAddon.Startup.TrackingStation, false)]
    public class TrackingStationAddon : KerbalConstructionTime
    {
        private Button.ButtonClickedEvent _originalCallback, _flyCallback;
        private Vessel _selectedVessel = null;

        public new void Start()
        {
            base.Start();
            if (KCT_GUI.IsPrimarilyDisabled)
                return;

            KCTDebug.Log("KCT_TS, Start");
            SpaceTracking trackingStation = FindObjectOfType<SpaceTracking>();
            if (trackingStation != null)
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
            _selectedVessel = null;
            var trackingStation = (SpaceTracking)FindObjectOfType(typeof(SpaceTracking));
            _selectedVessel = trackingStation.SelectedVessel;

            if (_selectedVessel == null)
            {
                Debug.LogError("[KCT] No Vessel selected.");
                return;
            }

            bool canRecoverToSPH = _selectedVessel.IsRecoverable && _selectedVessel.IsClearToSave() == ClearToSaveStatus.CLEAR;

            string reqTech = PresetManager.Instance.ActivePreset.GeneralSettings.VABRecoveryTech;
            bool canRecoverToVAB = _selectedVessel.IsRecoverable &&
                                   _selectedVessel.IsClearToSave() == ClearToSaveStatus.CLEAR &&
                                   (_selectedVessel.situation == Vessel.Situations.PRELAUNCH ||
                                    string.IsNullOrEmpty(reqTech) ||
                                    ResearchAndDevelopment.GetTechnologyState(reqTech) == RDTech.State.Available);

            int cnt = 2;
            if (canRecoverToSPH) cnt++;
            if (canRecoverToVAB) cnt++;

            DialogGUIBase[] options = new DialogGUIBase[cnt];
            cnt = 0;
            if (canRecoverToSPH)
            {
                options[cnt++] = new DialogGUIButton("Recover to SPH", RecoverToSPH);
            }
            if (canRecoverToVAB)
            {
                options[cnt++] = new DialogGUIButton("Recover to VAB", RecoverToVAB);
            }
            options[cnt++] = new DialogGUIButton("Normal recovery", DoNormalRecovery);
            options[cnt] = new DialogGUIButton("Cancel", () => { });

            var diag = new MultiOptionDialog("scrapVesselPopup", "Do you want KCT to do the recovery?", "Recover Vessel", null, options: options);
            PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), diag, false, HighLogic.UISkin);
        }
    }
}
