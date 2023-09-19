using UnityEngine;
using System.Collections.Generic;
using ToolbarControl_NS;
using System;

namespace RP0
{
    public abstract class HostedSingleton
    {
        protected MonoBehaviour _host = null;
        public MonoBehaviour Host => _host;

        protected static HostedSingleton _instance = null;
        public static HostedSingleton Instance => _instance;

        public virtual void Awake() { }

        public virtual void Start() { }

        public virtual void OnDestroy() { }

        public HostedSingleton(MonoBehaviour host)
        {
            _host = host;
            _instance = this;
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

            // Cache facility ID -> enum
            foreach (var kvp in ScenarioUpgradeableFacilities.facilityStrings)
                Database.FacilityIDToFacility[kvp.Value] = kvp.Key;

            _singletons.Add(new LocalizationHandler(this));
            _singletons.Add(new HideEmptyNodes(this));
            _singletons.Add(new DifficultyPresetChanger(this));
            _singletons.Add(new RFTagApplier(this));
            _singletons.Add(new KCTSettings(this));

            foreach (var s in _singletons)
            {
                try
                {
                    s.Awake();
                }
                catch (Exception e)
                {
                    RP0Debug.LogError($"Exception awaking {s.GetType()}: {e}");
                }
            }
        }

        public void Start()
        {
            ToolbarControl.RegisterMod(KerbalConstructionTimeData._modId, KerbalConstructionTimeData._modName);

            foreach (var s in _singletons)
            {
                try
                {
                    s.Awake();
                }
                catch (Exception e)
                {
                    RP0Debug.LogError($"Exception starting {s.GetType()}: {e}");
                }
            }
        }

        public void OnDestroy()
        {
            for (int i = _singletons.Count; i-- > 0;)
            {
                try
                {
                    _singletons[i].OnDestroy();
                }
                catch (Exception e)
                {
                    RP0Debug.LogError($"Exception starting {(_singletons[i]).GetType()}: {e}");
                }
                _singletons[i] = null;
            }
            _singletons.Clear();
        }
    }
}
