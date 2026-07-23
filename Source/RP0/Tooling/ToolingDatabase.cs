using System;
using System.Collections.Generic;

namespace RP0
{
    public class ToolingEntry
    {
        public float Value { get; set; }
        public List<ToolingEntry> Children { get; } = new List<ToolingEntry>();

        // Set on merged-leaf entries to record which DB type(s) supplied this exact
        // (mass, diameter, length) combination. Null on entries that live in the database
        // proper, and on non-leaf merged entries.
        public HashSet<string> Sources { get; set; }

        public ToolingEntry(float value)
        {
            Value = value;
        }
    }
    public class ToolingDatabase
    {
        public const float toolingMargin = .04f;
        protected const float comparisonHigh = 1.00f + toolingMargin;
        protected const float comparisonLow = 1.00f - toolingMargin;
        protected const float epsilon = 1e-6f;

        // Band within which two entries being merged for display count as the same value. This is
        // float round-trip noise only -- unlike toolingMargin it carries no physical meaning.
        protected const float mergeEpsilon = 1e-5f;

        public static float GetLowComparison(float value) => value * (comparisonLow - epsilon);

        public static float GetHighComparison(float value) => value * (comparisonHigh + epsilon);

        protected static int EpsilonCompare(float a, float b)
        {
            if (a > GetLowComparison(b) && a < GetHighComparison(b))
                return 0;

            return a.CompareTo(b);
        }

        /// <summary>
        /// Matches two entries being merged for display. Deliberately NOT <see cref="EpsilonCompare"/>:
        /// that treats anything within the 4% tooling margin as equal, which is right for "does this
        /// tooling cover that part?" but wrong for building a list -- merging a 90t entry into a 93t
        /// one relabels it 93t and drops it as a row of its own. The band is relative to the larger
        /// operand so the result does not depend on which side is compared first.
        /// </summary>
        /// <param name="a">First value.</param>
        /// <param name="b">Second value.</param>
        /// <returns>0 when the two are the same to within <see cref="mergeEpsilon"/>, else their relative order.</returns>
        protected static int ExactCompare(float a, float b)
        {
            if (Math.Abs(a - b) <= mergeEpsilon * Math.Max(Math.Abs(a), Math.Abs(b)))
                return 0;

            return a.CompareTo(b);
        }

        // Cached so passing a comparator to GetEntryIndex doesn't allocate a delegate per call --
        // it runs per-part in the editor via GetToolingLevel.
        private static readonly Comparison<float> _epsilonComparison = EpsilonCompare;
        private static readonly Comparison<float> _exactComparison = ExactCompare;

        public static Dictionary<string, List<ToolingEntry>> toolings = new Dictionary<string, List<ToolingEntry>>();

        // Bumps on every mutation (UnlockTooling adding entries, Load wiping the table).
        // Lets the UI cache derived views (e.g. merged grouped entries) and invalidate
        // without scanning the whole tree each frame.
        public static int Generation { get; private set; }

        protected static int GetEntryIndex(float value, List<ToolingEntry> list, out int min)
            => GetEntryIndex(value, list, out min, _epsilonComparison);

        /// <summary>Binary search over the ascending entry list.</summary>
        /// <param name="value">Value to look for.</param>
        /// <param name="list">Entries to search, ascending.</param>
        /// <param name="min">Receives the index the value would be inserted at when there's no match.</param>
        /// <param name="compare">
        /// Decides what counts as the same entry: <see cref="EpsilonCompare"/> for coverage queries
        /// (a slightly smaller tooling still covers a part), <see cref="ExactCompare"/> when merging
        /// types together for display.
        /// </param>
        /// <returns>Index of the matching entry, or -1 when there is none.</returns>
        protected static int GetEntryIndex(float value, List<ToolingEntry> list, out int min, Comparison<float> compare)
        {
            min = 0;
            int max = list.Count - 1;
            while (min <= max)
            {
                int mid = (min + max) / 2;
                int cmp = compare(value, list[mid].Value);
                if (cmp == 0) return mid;
                if (cmp > 0) min = mid + 1;
                else max = mid - 1;
            }
            return -1;
        }

        /// <summary>
        /// Compares two tooling sizes and checks whether their diameter and length are within a predefined epsilon (currently 4%).
        /// </summary>
        /// <param name="diam1">Diameter of tooling 1</param>
        /// <param name="len1">Length of tooling 1</param>
        /// <param name="diam2">Diameter of tooling 2</param>
        /// <param name="len2">Length of tooling 2</param>
        /// <returns>True if the two tooling sizes are considered the same.</returns>
        public static bool IsSameSize(float diam1, float len1, float diam2, float len2)
        {
            return EpsilonCompare(diam1, diam2) == 0 && EpsilonCompare(len1, len2) == 0;
        }

        public static int GetToolingLevel(string type, params float[] parameters)
        {
            if (!ToolingManager.Instance.toolingEnabled)
            {
                return parameters.Length;
            }

            List<ToolingEntry> entries;
            var level = 0;
            if (toolings.TryGetValue(type, out entries))
            {
                for (int i = 0; i < parameters.Length; ++i)
                {
                    var entryIndex = GetEntryIndex(parameters[i], entries, out _);
                    if (entryIndex == -1)
                    {
                        break;
                    }
                    level++;
                    entries = entries[entryIndex].Children;
                }
            }

            return level;
        }

        /// <summary>
        /// Returns a freshly-merged tree combining the entries of every supplied type. Used by the
        /// tooling UI to collapse grouped types (e.g. all Avionics-N* tech levels under one
        /// Avionics-N entry) without mutating the underlying database.
        /// </summary>
        /// <param name="types">Database keys whose entries are combined.</param>
        public static List<ToolingEntry> GetMergedEntries(IEnumerable<string> types)
        {
            var merged = new List<ToolingEntry>();
            foreach (var type in types)
            {
                if (toolings.TryGetValue(type, out var entries))
                {
                    foreach (var entry in entries)
                        MergeEntryInto(merged, entry, type);
                }
            }
            return merged;
        }

        private static void MergeEntryInto(List<ToolingEntry> target, ToolingEntry source, string sourceType)
        {
            // Exact match, not the tooling margin: target accumulates entries from several types, and
            // two of them landing within 4% of each other are still two distinct toolings.
            int existing = GetEntryIndex(source.Value, target, out int insertionIndex, _exactComparison);
            ToolingEntry dest;
            if (existing >= 0)
            {
                dest = target[existing];
            }
            else
            {
                dest = new ToolingEntry(source.Value);
                target.Insert(insertionIndex, dest);
            }
            if (source.Children.Count == 0)
            {
                if (dest.Sources == null) dest.Sources = new HashSet<string>();
                dest.Sources.Add(sourceType);
            }
            else
            {
                foreach (var child in source.Children)
                    MergeEntryInto(dest.Children, child, sourceType);
            }
        }

        public static bool UnlockTooling(string type, params float[] parameters)
        {
            var toolingUnlocked = false;
            if (!toolings.TryGetValue(type, out var entries))
            {
                entries = new List<ToolingEntry>();
                toolings[type] = entries;
            }

            for (int i = 0; i < parameters.Length; ++i)
            {
                var entryIndex = GetEntryIndex(parameters[i], entries, out var insertionIndex);
                if (entryIndex == -1)
                {
                    entries.Insert(insertionIndex, new ToolingEntry(parameters[i]));
                    entryIndex = insertionIndex;
                    toolingUnlocked = true;
                }
                entries = entries[entryIndex].Children;
            }

            if (toolingUnlocked) Generation++;
            return toolingUnlocked;
        }


        public static void Load(ConfigNode node)
        {
            LoadDBFromNode(node);
        }

        private static void LoadDBFromNode(ConfigNode node)
        {
            toolings.Clear();
            Generation++;

            if (node == null) return;

            foreach (var typeNode in node.GetNodes("TYPE"))
            {
                string type = typeNode.GetValue("type");
                if (string.IsNullOrEmpty(type))
                {
                    continue;
                }

                var entries = new List<ToolingEntry>();
                LoadEntries(typeNode, entries);
                if (entries.Count > 0)
                {
                    toolings[type] = entries;
                }
            }
        }

        private static void LoadEntries(ConfigNode node, List<ToolingEntry> entries)
        {
            foreach (var entryNode in node.GetNodes("ENTRY"))
            {
                var tmp = 0f;
                if (!entryNode.TryGetValue("value", ref tmp))
                {
                    continue;
                }

                var entry = new ToolingEntry(tmp);
                LoadEntries(entryNode, entry.Children);
                entries.Add(entry);
            }
        }

        public static void Save(ConfigNode node)
        {
            foreach (var typeToEntries in toolings)
            {
                var typeNode = node.AddNode("TYPE");
                typeNode.AddValue("type", typeToEntries.Key);

                var entries = typeToEntries.Value;
                SaveEntries(typeNode, entries);
            }
        }

        private static void SaveEntries(ConfigNode typeNode, List<ToolingEntry> entries)
        {
            foreach (var entry in entries)
            {
                var entryNode = typeNode.AddNode("ENTRY");
                entryNode.AddValue("value", entry.Value.ToString("G17"));
                SaveEntries(entryNode, entry.Children);
            }
        }
    }
}
