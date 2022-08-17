using UnityEngine;

namespace RP0
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class EventRegisterer : MonoBehaviour
    {
        public void Awake()
        {
            DontDestroyOnLoad(this);
        }
    }
}
