using UnityEngine;
using System.Collections.Generic;

namespace RP0
{
    public abstract class HostedSingleton
    {
        protected MonoBehaviour _host = null;
        public MonoBehaviour Host => _host;

        protected static HostedSingleton _instance = null;
        public static HostedSingleton Instance => _instance;

        public abstract void Awake();

        public virtual void OnDestroy() { }

        public HostedSingleton(MonoBehaviour host)
        {
            _host = host;
            _instance = this;
            Awake();
        }
    }

    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class SingletonHost : MonoBehaviour
    {
        private List<HostedSingleton> _singletons = new List<HostedSingleton>();

        public void Awake()
        {
            DontDestroyOnLoad(this);

            RP0Debug.Log("SingletonHost Awake - registering events and running processes");

            // Subscribe to events from KSP and other mods.
            // This is done as early as possible for the scene change events to work when loading into a save from main menu.
            if (!KCTEvents.Instance.SubscribedToEvents)
            {
                KCTEvents.Instance.SubscribeToEvents();
            }

            _singletons.Add(new LocalizationHandler(this));
            _singletons.Add(new HideEmptyNodes(this));
            _singletons.Add(new DifficultyPresetChanger(this));
            _singletons.Add(new RFTagApplier(this));
        }

        public void OnDestroy()
        {
            for (int i = _singletons.Count; i-- > 0;)
            {
                _singletons[i].OnDestroy();
                _singletons[i] = null;
            }
            _singletons.Clear();
        }
    }
}
