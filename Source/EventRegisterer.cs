using UnityEngine;

namespace RP0
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class EventRegisterer : MonoBehaviour
    {
        public void Awake()
        {
            if (MaintenanceHandler.OnRP0MaintenanceChanged == null)
                MaintenanceHandler.OnRP0MaintenanceChanged = new EventVoid("OnRP0MaintenanceChanged");
        }
    }
}
