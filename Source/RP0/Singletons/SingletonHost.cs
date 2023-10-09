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

        public HostedSingleton(SingletonHost host)
        {
            _host = host;
            _instance = this;
        }
        
        
        public virtual void Awake() { }

        public virtual void Start() { }

        public virtual void OnDestroy() { }
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
            if (!SCMEvents.Instance.SubscribedToEvents)
            {
                SCMEvents.Instance.SubscribeToEvents();
            }

            // Poke Kerbalism
            KERBALISM.Settings.UseSamplingSunFactor = true;

            // Cache facility ID -> enum
            foreach (var kvp in ScenarioUpgradeableFacilities.facilityStrings)
                Database.FacilityIDToFacility[kvp.Value] = kvp.Key;

            List<Type> singletonTypes = KSPUtils.GetAllLoadedTypes<HostedSingleton>();
            foreach (var t in singletonTypes)
            {
                HostedSingleton s = (HostedSingleton)Activator.CreateInstance(t, new System.Object[] { this });
                _singletons.Add(s);
            }
            string logstr = "Found and added " + _singletons.Count + " singletons:";
            foreach (var s in singletonTypes)
                logstr += "\n" + s.FullName;
            RP0Debug.Log(logstr, true);

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
                    s.Start();
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
                    RP0Debug.LogError($"Exception destroying {(_singletons[i]).GetType()}: {e}");
                }
                _singletons[i] = null;
            }
            _singletons.Clear();
        }
    }
}
