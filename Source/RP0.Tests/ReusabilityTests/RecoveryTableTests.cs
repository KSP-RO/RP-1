using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using RP0.Tests.ReusabilityTests.Config;

namespace RP0.Tests.ReusabilityTests
{
    /// <summary>
    /// Generates the balance table for each tech tier and diffs it against the approved
    /// golden file. A deliberate balance change - to Formula, to the fixtures, or to the
    /// SCMData cfgs - shows up here as a reviewable diff; promoting the generated
    /// .received.md to .md is the explicit "yes, this is the new balance" step.
    /// </summary>
    [TestFixture]
    public class RecoveryTableTests
    {
        public static IEnumerable<TechTier> Tiers => TechTier.All;

        [TestCaseSource(nameof(Tiers))]
        public void Table_MatchesApproved(TechTier tier)
        {
            string received = ReusabilityTable.Render(tier);

            Directory.CreateDirectory(TestPaths.ApprovedDir);
            // Approved is shared across target frameworks on purpose: net48 and net8.0 must
            // produce identical tables, which makes this a floating-point parity check too.
            // Received is per-framework so a concurrent multi-target run cannot clobber it.
            string approvedPath = Path.Combine(TestPaths.ApprovedDir, $"RecoveryTable.{tier.Name}.md");
            string receivedPath = Path.Combine(TestPaths.ApprovedDir, $"RecoveryTable.{tier.Name}.{TestPaths.Tfm}.received.md");

            // Always visible with: dotnet test -l "console;verbosity=detailed"
            TestContext.Out.WriteLine(received);

            if (!File.Exists(approvedPath))
            {
                File.WriteAllText(receivedPath, received);
                Assert.Fail($"No approved table for the {tier.Name} tier.\n" +
                            $"Review the generated table and promote it:\n" +
                            $"  {receivedPath}\n" +
                            $"  -> {approvedPath}");
            }

            string approved = File.ReadAllText(approvedPath);
            if (Normalize(approved) == Normalize(received))
            {
                try
                {
                    if (File.Exists(receivedPath))
                        File.Delete(receivedPath);
                }
                catch (IOException)
                {
                    // Stale received file left behind is cosmetic; never fail a passing test on it.
                }
                return;
            }

            File.WriteAllText(receivedPath, received);
            Assert.Fail($"The {tier.Name} tier table changed.\n\n" +
                        ReusabilityTable.Diff(approved, received) +
                        $"\nIf this change is intended, promote:\n" +
                        $"  {receivedPath}\n" +
                        $"  -> {approvedPath}");
        }

        private static string Normalize(string text) => text.Replace("\r\n", "\n").TrimEnd();
    }
}
