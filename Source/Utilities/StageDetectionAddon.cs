using UnityEngine;

namespace RP0
{
    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    public class MainMenuAddon : MonoBehaviour
    {
        public void Start()
        {
            // Subscribe to events from KSP and other mods.
            // This is done as early as possible for the scene change events to work when loading into a save from main menu.
            if (!StageDetectionEvents.Instance.SubscribedToEvents)
            {
                StageDetectionEvents.Instance.SubscribeToEvents();
            }
        }
    }

    public class StageDetectionEvents
    {
        public static StageDetectionEvents Instance { get; private set; } = new StageDetectionEvents();
        public bool SubscribedToEvents { get; private set; }

        public StageDetectionEvents()
        {
            SubscribedToEvents = false;
        }

        public void SubscribeToEvents()
        {
            GameEvents.onEditorShipModified.Add(new EventData<ShipConstruct>.OnEvent(OnEditorShipModified));
            

            SubscribedToEvents = true;
        }

        void OnEditorShipModified(ShipConstruct sc)
        {
            var tree = new StageDetection().BuildTreeFromShipConstruct(sc);
            tree.printTree();
        }
    }
}