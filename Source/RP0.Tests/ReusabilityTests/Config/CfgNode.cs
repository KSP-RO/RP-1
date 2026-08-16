using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace RP0.Tests.ReusabilityTests.Config
{
    /// <summary>
    /// A minimal stand-in for KSP's ConfigNode, enough to read the SCMData balance cfgs.
    /// RP0.Tests has no KSP assemblies, so the real ConfigNode is unavailable; the accessor
    /// names and semantics here match the ones Database.RecoveryTechSettings uses
    /// (HasNode / GetNode / GetNodes / GetValue / TryGetValue-by-ref).
    ///
    /// Supported syntax: NAME on its own line or followed by '{', '}' to close, 'key = value'
    /// pairs, '//' line and trailing comments. That covers RecoveryLevels.cfg and
    /// RefurbishmentLevels.cfg; it is not a general ModuleManager-capable parser (no
    /// operators, no multi-line values).
    /// </summary>
    public sealed class CfgNode
    {
        public string Name { get; }

        private readonly List<KeyValuePair<string, string>> _values = new List<KeyValuePair<string, string>>();
        private readonly List<CfgNode> _nodes = new List<CfgNode>();

        public CfgNode(string name)
        {
            Name = name;
        }

        public IReadOnlyList<CfgNode> Nodes => _nodes;

        public bool HasNode(string name) => _nodes.Any(n => n.Name == name);

        public CfgNode GetNode(string name) => _nodes.FirstOrDefault(n => n.Name == name);

        public IEnumerable<CfgNode> GetNodes(string name) => _nodes.Where(n => n.Name == name);

        public string GetValue(string name) =>
            _values.Where(kv => kv.Key == name).Select(kv => kv.Value).FirstOrDefault();

        /// <summary>
        /// Mirrors KSP's ConfigNode.TryGetValue(string, ref double): leaves the target
        /// untouched and returns false when the key is missing or unparseable.
        /// </summary>
        public bool TryGetValue(string name, ref double value)
        {
            string raw = GetValue(name);
            if (raw == null)
                return false;

            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                return false;

            value = parsed;
            return true;
        }

        public static CfgNode Load(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Balance cfg not found: {path}", path);

            var root = new CfgNode("<root>");
            var stack = new Stack<CfgNode>();
            stack.Push(root);

            string pendingName = null;

            foreach (string rawLine in File.ReadAllLines(path))
            {
                string line = StripComment(rawLine).Trim();
                if (line.Length == 0)
                    continue;

                // "NAME {" on one line is legal in KSP cfgs; normalise it to the two-line form.
                if (line.Length > 1 && line.EndsWith("{", StringComparison.Ordinal))
                {
                    pendingName = line.Substring(0, line.Length - 1).Trim();
                    line = "{";
                }

                if (line == "{")
                {
                    if (pendingName == null)
                        throw new InvalidDataException($"Unnamed node opened in {path}");

                    var child = new CfgNode(pendingName);
                    stack.Peek()._nodes.Add(child);
                    stack.Push(child);
                    pendingName = null;
                    continue;
                }

                if (line == "}")
                {
                    if (stack.Count <= 1)
                        throw new InvalidDataException($"Unbalanced '}}' in {path}");

                    stack.Pop();
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq >= 0)
                {
                    string key = line.Substring(0, eq).Trim();
                    string value = line.Substring(eq + 1).Trim();
                    stack.Peek()._values.Add(new KeyValuePair<string, string>(key, value));
                    continue;
                }

                pendingName = line;
            }

            if (stack.Count != 1)
                throw new InvalidDataException($"Unclosed node in {path}");

            return root;
        }

        private static string StripComment(string line)
        {
            int idx = line.IndexOf("//", StringComparison.Ordinal);
            return idx < 0 ? line : line.Substring(0, idx);
        }
    }
}
