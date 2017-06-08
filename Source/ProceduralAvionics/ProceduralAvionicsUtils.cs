using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RP0.ProceduralAvionics
{
	class ProceduralAvionicsUtils
	{

		private const string logPreix = "[ProcAvi] - ";
		public static void Log(params string[] message)
		{
			var builder = StringBuilderCache.Acquire();
			builder.Append(logPreix);
			foreach (string part in message) {
				builder.Append(part);
			}
			UnityEngine.Debug.Log(builder.ToStringAndRelease());
		}

	}
}
