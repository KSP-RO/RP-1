using UnityEngine;

namespace KerbalConstructionTime
{

    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    public class MainMenuAddon : MonoBehaviour
    {
        public void Start()
        {
            KCTDebug.Log("MainMenuAddon Start called");

            // Subscribe to events from KSP and other mods.
            // This is done as early as possible for the scene change events to work when loading into a save from main menu.
            if (!KCTEvents.Instance.SubscribedToEvents)
            {
                KCTEvents.Instance.SubscribeToEvents();
            }
        }
    }
}
