using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;

namespace RP0.Tests.ReusabilityTests
{
    /// <summary>
    /// Locates the source tree at run time. The project dir is baked in as assembly
    /// metadata by RP0.Tests.csproj, so tests read the live GameData cfgs and write
    /// received tables next to their golden files rather than into bin/.
    /// </summary>
    public static class TestPaths
    {
        public static string ProjectDir { get; } = ResolveProjectDir();

        /// <summary>&lt;repo&gt;/Source/RP0.Tests/ReusabilityTests</summary>
        public static string TestsDir => Path.Combine(ProjectDir, "ReusabilityTests");

        /// <summary>&lt;repo&gt;/Source/RP0.Tests/ReusabilityTests/Approved</summary>
        public static string ApprovedDir => Path.Combine(TestsDir, "Approved");

        /// <summary>&lt;repo&gt;/GameData/RP-1/SCMData</summary>
        public static string SCMDataDir => Path.Combine(ProjectDir, "..", "..", "GameData", "RP-1", "SCMData");

        public static string SCMData(string fileName) => Path.GetFullPath(Path.Combine(SCMDataDir, fileName));

        /// <summary>
        /// Short moniker for the framework this run is executing on ("net48" / "net8.0").
        /// The project multi-targets, and both targets write received tables into the same
        /// source folder, so generated file names are qualified to keep them from colliding.
        /// Approved file names are NOT qualified: both runtimes must match the same golden.
        /// </summary>
        public static string Tfm { get; } = ResolveTfm();

        private static string ResolveTfm()
        {
            string name = Assembly.GetExecutingAssembly()
                                  .GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName
                          ?? string.Empty;

            // ".NETFramework,Version=v4.8" -> net48 ; ".NETCoreApp,Version=v8.0" -> net8.0
            if (name.StartsWith(".NETFramework", StringComparison.Ordinal))
                return "net" + VersionDigits(name).Replace(".", string.Empty);
            if (name.StartsWith(".NETCoreApp", StringComparison.Ordinal))
                return "net" + VersionDigits(name);

            return "unknown";
        }

        private static string VersionDigits(string frameworkName)
        {
            int v = frameworkName.IndexOf("Version=v", StringComparison.Ordinal);
            return v < 0 ? "?" : frameworkName.Substring(v + "Version=v".Length);
        }

        private static string ResolveProjectDir()
        {
            string dir = Assembly.GetExecutingAssembly()
                                 .GetCustomAttributes<AssemblyMetadataAttribute>()
                                 .FirstOrDefault(a => a.Key == "ProjectDir")?.Value;

            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                throw new InvalidOperationException(
                    "RP0.Tests could not locate its project directory. The ProjectDir assembly " +
                    "metadata is written by RP0.Tests.csproj; rebuild the project.");

            return dir;
        }
    }
}
