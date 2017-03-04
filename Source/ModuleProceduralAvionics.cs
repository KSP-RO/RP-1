using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RP0
{
	class ModuleProceduralAvionics : ModuleAvionics, IPartMassModifier
	{
		[KSPField]
		public float tonnageToMassRatio = 10; // default is 10 tons of control to one ton of part mass.  A higher number is more efficient.

		[KSPField(isPersistant = true, guiName = "Tonnage", guiActive = false, guiActiveEditor = true, guiUnits = "T"),
		 UI_FloatEdit(scene = UI_Scene.Editor, minValue = 0f, incrementLarge = 10f, incrementSmall = 1f, incrementSlide = 0.1f, sigFigs = 2, unit = "T")]
		public float proceduralMassLimit = 1;

		protected override float getInternalMassLimit()
		{
			return proceduralMassLimit;
		}

		protected float CalculateNewMass()
		{
			//TODO: add in EC mass
			//TODO: change resource rate (we might need to make a ModuleProceduralCommand

			//UnityEngine.Debug.Log("[ProcAvi] - current mass is " + part.mass);
			float newMass = proceduralMassLimit / tonnageToMassRatio;
			UnityEngine.Debug.Log("[ProcAvi] - setting new mass to " + newMass);
			return newMass;
			//part.mass = newMass;
			//UnityEngine.Debug.Log("[ProcAvi] - mass is now " + part.mass);
		}

		public new void Update()
		{
			if (HighLogic.LoadedSceneIsEditor)
			{
				//CalculateNewMass();
			}
			base.Update();
		}

		public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
		{
			return CalculateNewMass();
		}

		public ModifierChangeWhen GetModuleMassChangeWhen()
		{
			return ModifierChangeWhen.FIXED;
		}
	}
}