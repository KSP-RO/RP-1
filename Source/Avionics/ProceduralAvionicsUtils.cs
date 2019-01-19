using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RP0.ProceduralAvionics
{
	static class ProceduralAvionicsUtils
	{
		private static bool _enableLogging = false;

		private const string logPreix = "[ProcAvi] - ";
		public static void Log(params string[] message)
		{
			if (_enableLogging) {
				var builder = StringBuilderCache.Acquire();
				builder.Append(logPreix);
				foreach (string part in message) {
					builder.Append(part);
				}
				UnityEngine.Debug.Log(builder.ToStringAndRelease());
			}
		}

		public static void Log(params object[] parts)
		{
			if (_enableLogging) {
				var builder = StringBuilderCache.Acquire();
				builder.Append(logPreix);
				foreach (object part in parts) {
					builder.Append(part.ToString());
				}
				UnityEngine.Debug.Log(builder.ToStringAndRelease());
			}
		}

	}
}
