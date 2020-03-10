//Taken from ProceduralParts
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
//using DeftTech.DuckTyping;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KSPAPIExtensions
{
    /// <summary>
    /// Flags to determine relationship between two parts.
    /// </summary>
    [Flags]
    public enum PartRelationship
    {
        Vessel = 0x1,
        Self = 0x2,
        Symmetry = 0x4,
        Decendent = 0x8,
        Child = 0x10,
        Ancestor = 0x20,
        Parent = 0x40,
        Sibling = 0x80,
        Unrelated = 0x100,
        Unknown = 0x0,

        AnyOnVessel = Vessel | Self | Symmetry | Decendent | Child | Ancestor | Parent | Sibling,
        AnyPart = AnyOnVessel | Unrelated,
    }

    /// <summary>
    /// Flags to filter particular game scenes.
    /// </summary>
    [Flags]
    public enum GameSceneFilter
    {
        Loading = 1 << GameScenes.LOADING,
        MainMenu = 1 << GameScenes.MAINMENU,
        SpaceCenter = 1 << GameScenes.SPACECENTER,
        VAB = 1 << GameScenes.EDITOR,
        SPH = 1 << GameScenes.EDITOR,
        Flight = 1 << GameScenes.FLIGHT,
        TrackingStation = 1 << GameScenes.TRACKSTATION,
        Settings = 1 << GameScenes.SETTINGS,
        Credits = 1 << GameScenes.CREDITS,

        AnyEditor = VAB | SPH,
        AnyEditorOrFlight = AnyEditor | Flight,
        AnyInitializing = 0xFFFF & ~(AnyEditor | Flight),
        Any = 0xFFFF
    }

    public static class PartUtils
    {
        private static FieldInfo windowListField;

        /// <summary>
        /// Find the UIPartActionWindow for a part. Usually this is useful just to mark it as dirty.
        /// </summary>
        public static UIPartActionWindow FindActionWindow(this Part part)
        {
            if (part == null)
                return null;

            // We need to do quite a bit of piss-farting about with reflection to 
            // dig the thing out. We could just use Object.Find, but that requires hitting a heap more objects.
            UIPartActionController controller = UIPartActionController.Instance;
            if (controller == null)
                return null;

            if (windowListField == null)
            {
                Type cntrType = typeof(UIPartActionController);
                foreach (FieldInfo info in cntrType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                {
                    if (info.FieldType == typeof(List<UIPartActionWindow>))
                    {
                        windowListField = info;
                        goto foundField;
                    }
                }
                Debug.LogWarning("*PartUtils* Unable to find UIPartActionWindow list");
                return null;
            }
            foundField:

            List<UIPartActionWindow> uiPartActionWindows = (List<UIPartActionWindow>)windowListField.GetValue(controller);
            if (uiPartActionWindows == null)
                return null;

            return uiPartActionWindows.FirstOrDefault(window => window != null && window.part == part);
        }

        /// <summary>
        /// If this part is a symmetry clone of another part, this method will return the original part.
        /// </summary>
        /// <param name="part">The part to find the original of</param>
        /// <returns>The original part, or the part itself if it was the original part</returns>
        [Obsolete("This don't seem to work with the current version of KSP", false)]
        public static Part GetSymmetryCloneOriginal(this Part part)
        {
            if (!part.isClone || part.symmetryCounterparts == null || part.symmetryCounterparts.Count == 0)
                return part;

            // Symmetry counterparts always are named xxxx(Clone) if they are cloned from xxxx. So the shortest name is the one.
            int nameLength = part.transform.name.Length;
            foreach (Part other in part.symmetryCounterparts)
            {
                if (other.transform.name.Length < nameLength)
                    return other;
            }
            return part;
        }

        public static bool IsSurfaceAttached(this Part part)
        {
            return part.srfAttachNode != null && part.srfAttachNode.attachedPart == part.parent;
        }

        /// <summary>
        /// Find the relationship between two parts.
        /// </summary>
        public static PartRelationship RelationTo(this Part part, Part other)
        {
            if (other == null || part == null)
                return PartRelationship.Unknown;

            if (other == part)
                return PartRelationship.Self;
            if (part.localRoot != other.localRoot)
                return PartRelationship.Unrelated;
            if (part.parent == other)
                return PartRelationship.Child;
            if (other.parent == part)
                return PartRelationship.Parent;
            if (other.parent == part.parent)
                return PartRelationship.Sibling;
            for (Part tmp = part.parent; tmp != null; tmp = tmp.parent)
                if (tmp == other)
                    return PartRelationship.Decendent;
            for (Part tmp = other.parent; tmp != null; tmp = tmp.parent)
                if (tmp == part)
                    return PartRelationship.Ancestor;
            if (part.localRoot == other.localRoot)
                return PartRelationship.Vessel;
            return PartRelationship.Unrelated;
        }

        /// <summary>
        /// Test if two parts are related by a set of criteria. Because PartRelationship is a flags
        /// enumeration, multiple flags can be tested at the same time.
        /// </summary>
        public static bool RelationTest(this Part part, Part other, PartRelationship relation)
        {
            if (relation == PartRelationship.Unknown)
                return true;
            if (part == null || other == null)
                return false;

            if (TestFlag(relation, PartRelationship.Self) && part == other)
                return true;
            if (TestFlag(relation, PartRelationship.Vessel) && part.localRoot == other.localRoot)
                return true;
            if (TestFlag(relation, PartRelationship.Unrelated) && part.localRoot != other.localRoot)
                return true;
            if (TestFlag(relation, PartRelationship.Sibling) && part.parent == other.parent)
                return true;
            if (TestFlag(relation, PartRelationship.Ancestor))
            {
                for (Part upto = other.parent; upto != null; upto = upto.parent)
                    if (upto == part)
                        return true;
            }
            else if (TestFlag(relation, PartRelationship.Parent) && part == other.parent)
                return true;

            if (TestFlag(relation, PartRelationship.Decendent))
            {
                for (Part upto = part.parent; upto != null; upto = upto.parent)
                    if (upto == other)
                        return true;
            }
            else if (TestFlag(relation, PartRelationship.Child) && part.parent == other)
                return true;

            if (TestFlag(relation, PartRelationship.Symmetry))
                return other.symmetryCounterparts.Any(sym => part == sym);
            return false;
        }

        internal static bool TestFlag(this PartRelationship e, PartRelationship flags)
        {
            return (e & flags) == flags;
        }

        /// <summary>
        /// Convert GameScene enum into GameSceneFilter
        /// </summary>
        public static GameSceneFilter AsFilter(this GameScenes scene)
        {
            return (GameSceneFilter)(1 << (int)scene);
        }

        /// <summary>
        /// True if the current game scene matches the filter.
        /// </summary>
        public static bool IsLoaded(this GameSceneFilter filter)
        {
            return (int)(filter & HighLogic.LoadedScene.AsFilter()) != 0;
        }

        /// <summary>
        /// Register an 'OnUpdate' method for use in the editor.
        /// This should be done in the OnAwake method of the module, and will ensure that all modules have the
        /// registered method called in order of declaration of the module in the part file.
        /// </summary>
        /// <param name="module">Module that is being registered.</param>
        /// <param name="action">Method to call</param>
        //public static void RegisterOnUpdateEditor(this PartModule module, Action action)
        //{
        //	if (!HighLogic.LoadedSceneIsEditor)
        //		return;
        //	Part part = module.part;
        //	IOnEditorUpdateUtility utility;
        //	foreach (MonoBehaviour c in part.GetComponents<MonoBehaviour>())
        //	{
        //		if (c.GetType().FullName == typeof (OnEditorUpdateUtility).FullName)
        //		{
        //			utility = DuckTyping.Cast<IOnEditorUpdateUtility>(c);
        //			goto found;
        //		}
        //	}
        //
        //	PartModule onEditorUpdate = (PartModule)part.gameObject.AddComponent(OnEditorUpdateUtility.LatestVersion);
        //	utility = DuckTyping.Cast<IOnEditorUpdateUtility>(onEditorUpdate);
        //	part.Modules.Add(onEditorUpdate); 
        //
        //	found:
        //		utility.AddOnUpdate(module, action);
        //}
    }

    internal interface IOnEditorUpdateUtility
    {
        void AddOnUpdate(PartModule module, Action action);
    }

    //internal class OnEditorUpdateUtility : PartModule, IOnEditorUpdateUtility
    //{
    //	public static readonly Type LatestVersion = SystemUtils.VersionTaggedType(SystemUtils.TypeElectionWinner(typeof (OnEditorUpdateUtility), "KSPAPIExtensions"));
    //
    //	public override void OnStart(StartState state)
    //	{
    //		started = true;
    //		part.Modules.Remove(this);
    //	}
    //
    //	private class ModAction : IComparable<ModAction>
    //	{
    //		public PartModule module;
    //		public Action action;
    //
    //		private int Index
    //		{
    //			get
    //			{
    //				return module.part.Modules.IndexOf(module);
    //			}
    //		}
    //
    //		public int CompareTo(ModAction other)
    //		{
    //			return Index.CompareTo(other.Index);
    //		}
    //	}
    //
    //	private readonly List<ModAction> modules = new List<ModAction>();
    //	private bool started;
    //
    //	public void AddOnUpdate(PartModule module, Action action)
    //	{
    //		ModAction item = new ModAction
    //		{
    //			module = module,
    //			action = action,
    //		};
    //		int insert = modules.BinarySearch(item);
    //		if (insert < 0)
    //			modules.Insert(~insert, item);
    //	}
    //
    //
    //	public void Update()
    //	{
    //		if (!started)
    //			return;
    //
    //		for (int i = 0; i < modules.Count; i++)
    //		{
    //			ModAction action = modules[i];
    //			if (!action.module)
    //				modules.RemoveAt(i--);
    //			else if (action.module.enabled)
    //				action.action();
    //		}
    //	}
    //}

    /// <summary>
    /// KSPAddon with equality checking using an additional type parameter. Fixes the issue where AddonLoader prevents multiple start-once addons with the same start scene.
    /// </summary>
    public class KSPAddonFixed : KSPAddon, IEquatable<KSPAddonFixed>
    {
        private readonly Type type;

        public KSPAddonFixed(Startup startup, bool once, Type type)
            : base(startup, once)
        {
            this.type = type;
        }

        public override bool Equals(object obj)
        {
            if (obj.GetType() != GetType()) { return false; }
            return Equals((KSPAddonFixed)obj);
        }

        public bool Equals(KSPAddonFixed other)
        {
            if (once != other.once) { return false; }
            if (startup != other.startup) { return false; }
            if (type != other.type) { return false; }
            return true;
        }

        public override int GetHashCode()
        {
            // ReSharper disable NonReadonlyFieldInGetHashCode
            return startup.GetHashCode() ^ once.GetHashCode() ^ type.GetHashCode();
            // ReSharper restore NonReadonlyFieldInGetHashCode
        }
    }
}

