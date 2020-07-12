using UnityEngine;

namespace KerbalConstructionTime
{
    public class KCTOnLoadError
    {
        public bool OnLoadCalled, OnLoadFinished, AlertFired;
        private const int _timeout = 100;
        private int _timer = 0;

        public bool HasErrored()
        {
            if (_timer >= _timeout)
            {
                return (OnLoadCalled && !OnLoadFinished);
            }
            else if (_timer >= 0)
            {
                _timer++;
            }
            return false;
        }

        public void OnLoadStart()
        {
            KCTDebug.Log("OnLoad Started");
            OnLoadCalled = true;
            OnLoadFinished = false;
            _timer = 0;
            AlertFired = false;
        }

        public void OnLoadFinish()
        {
            OnLoadCalled = false;
            OnLoadFinished = true;
            _timer = -1;
            KCTDebug.Log("OnLoad Completed");
        }

        public void FireAlert()
        {
            if (!AlertFired)
            {
                AlertFired = true;
                Debug.LogError("[KCT] ERROR! An error while KCT loading data occurred. Things will be seriously broken!");
                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "errorPopup", "Error Loading KCT Data", "ERROR! An error occurred while loading KCT data. Things will be seriously broken! Please report this error to RP-1 GitHub and attach the log file. The game will be UNPLAYABLE in this state!", "Understood", false, HighLogic.UISkin);

                //Enable debug messages for future reports
                KCTGameStates.Settings.Debug = true;
                KCTGameStates.Settings.Save();
            }
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
