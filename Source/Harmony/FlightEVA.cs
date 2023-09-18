using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace RP0
{
    /// <summary>
    /// Prevents EVAs being added to flight log when they happen at homeworld while situation < orbit.
    /// </summary>
    [HarmonyPatch(typeof(FlightEVA))]
	internal class PatchFlightEVA
	{
		[HarmonyTranspiler]
		[HarmonyPatch("onGoForEVA")]
		internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
		{
			int startIndex = -1;
			int endIndex = -1;

			var codes = new List<CodeInstruction>(instructions);

			// Finds the start and end index of the following piece of code:
			// this.pCrew.flightLog.AddEntryUnique(FlightLog.EntryType.ExitVessel, this.fromPart.vessel.orbit.referenceBody.name);
			for (int i = 0; i < codes.Count; i++)
			{
				if (codes[i].opcode == OpCodes.Callvirt &&
					codes[i].operand.ToString() == "Void AddEntryUnique(EntryType, System.String)")
				{
					endIndex = i;

					for (int j = i - 1; j >= 0; j--)
					{
						if (codes[j].opcode == OpCodes.Ldfld &&
							codes[j].operand.ToString() == "ProtoCrewMember pCrew")
						{
							startIndex = j - 1;    // include 1 more opcode (ldarg.0 / this) before the current one
							break;
						}
					}

					break;
				}
			}

			if (startIndex > -1 && endIndex > -1)
			{
				// Remove everything after the ldarg.0 call since this has a tag
				codes.RemoveRange(startIndex + 1, endIndex - startIndex);
				CodeInstruction call = CodeInstruction.Call(typeof(PatchFlightEVA), "AddEntryUniquePatched");
				codes.Insert(startIndex + 1, call);
			}

			return codes.AsEnumerable();
		}

		public static void AddEntryUniquePatched(FlightEVA instance)
		{
			var fromPart = (Part)AccessTools.Field(typeof(FlightEVA), "fromPart").GetValue(instance);
			Vessel v = fromPart.vessel;
			if (!v.orbit.referenceBody.isHomeWorld ||
				(v.orbit.referenceBody.isHomeWorld && v.situation > Vessel.Situations.SUB_ORBITAL))
			{
				var pCrew = (ProtoCrewMember)AccessTools.Field(typeof(FlightEVA), "pCrew").GetValue(instance);
				pCrew.flightLog.AddEntryUnique(FlightLog.EntryType.ExitVessel, v.orbit.referenceBody.name);
			}
		}
	}
}
