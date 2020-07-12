using UnityEngine;
using ToolbarControl_NS;

namespace KerbalConstructionTime
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class RegisterToolbar : MonoBehaviour
    {
        void Start()
        {
            ToolbarControl.RegisterMod(KCTGameStates._modId, KCTGameStates._modName);
        }
    }
}