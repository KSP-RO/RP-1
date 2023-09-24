using System;
using System.Collections.Generic;
using UniLinq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using UnityEngine.Profiling;

namespace RP0.DataTypes
{
    public class PersistentCompressedConfigNode : IConfigNode
    {
        const int _ChunkSize = 32720;
        const string _ValueName = "serialized";
        
        protected byte[] _bytes = null;
        protected ConfigNode _node = null;
        public virtual ConfigNode Node
        {
            get
            {
                if (_node == null)
                {
                    if(!Decompress())
                        return null;
                }

                return _node;
            }

            set
            {
                _bytes = null;
                _node = value;
            }
        }

        public bool IsEmpty => _node == null && _bytes == null;

        public PersistentCompressedConfigNode() { }

        public PersistentCompressedConfigNode(ConfigNode node, bool compress)
        {
            _node = node;
            if (compress)
                CompressAndRelease();
        }

        protected bool Decompress()
        {
            if (_bytes == null)
                return false;

            Profiler.BeginSample("RP0Decompress");
            //UnityEngine.Debug.Log("@@Extracting Shipnode!! Stack: " + Environment.StackTrace);
            string s = ObjectSerializer.UnZip(_bytes);
            _node = ConfigNode.Parse(s);
            // Parsing wraps the node in a rootnode. That's fine, because it means the node's name is saved.
            // So let's extract it out.
            _node = _node.nodes[0];
            //UnityEngine.Debug.Log("Resulting node:\n" + _node.ToString());

            Profiler.EndSample();
            return true;
        }

        protected void Compress()
        {
            Profiler.BeginSample("RP0Compress");
            _bytes = ObjectSerializer.Zip(_node.ToString());
            Profiler.EndSample();
        }

        public void CompressAndRelease()
        {
            if (_node == null || (Programs.ProgramHandler.Settings != null && Programs.ProgramHandler.Settings.doNotCompressData))
                return;

            Compress();
            _node = null;
        }

        protected unsafe void WriteChunks(ConfigNode node, string data)
        {
            int len = data.Length;
            int index = 0;
            fixed (char* pszData = data)
            {
                for (int i = 0; i < len; ++i)
                {
                    char c = pszData[i];
                    if (c == '+')
                        pszData[i] = '-';
                    else if (c == '/')
                        pszData[i] = '_';
                }

                for(;;)
                {
                    int length;
                    bool last;
                    int remaining = len - index;
                    if (remaining < _ChunkSize)
                    {
                        last = true;
                        length = remaining;
                    }
                    else
                    {
                        last = false;
                        length = _ChunkSize;
                    }
                    string chunkStr = new string(pszData, index, length);
                    node.AddValue(_ValueName, chunkStr);
                    if (last)
                        break;

                    index += length;
                }
            }
        }

        protected unsafe void LoadData(string s)
        {
            int len = s.Length;
            fixed (char* pszData = s)
            {
                for (int i = 0; i < len; ++i)
                {
                    char c = pszData[i];
                    if (c == '-')
                        pszData[i] = '+';
                    else if (c == '_')
                        pszData[i] = '/';
                }
            }
            int mod = len % 4;
            if (mod != 0)
            {
                UnityEngine.Debug.LogError("[RP-0] error: base64 string length is not divisble by 4! Padding.");
                s += new string('=', 4 - mod); // yuck, concatenation sucks here.
            }
            _bytes = ObjectSerializer.Base64Decode(s);
        }

        public void Copy(PersistentCompressedConfigNode src)
        {
            if (src._node != null)
                _node = src._node.CreateCopy();
            if (src._bytes != null)
                _bytes = src._bytes;
        }

        public virtual void Load(ConfigNode node)
        {
            if (node.values.Count == 0 && node.nodes.Count == 0)
                return;

            if (node.nodes.Count > 0)
            {
                _node = node.nodes[0];
            }

            var sb = new StringBuilder(_ChunkSize);
            for (int i = 0; i < node._values.Count; ++i)
            {
                var v = node._values[i];
                switch (v.name)
                {
                    case _ValueName:
                        sb.Append(v.value);
                        break;
                }
            }
            if (sb.Length == 0)
                return;
            
            string s = sb.ToString();
            sb.Clear();
            LoadData(s);
        }

        public void Save(ConfigNode node)
        {
            // Special handling: allow storing the node as uncompressed
            if (Programs.ProgramHandler.Settings != null && Programs.ProgramHandler.Settings.doNotCompressData)
            {
                if (_node != null || Decompress())
                {
                    node.AddNode(_node);
                    return;
                }
            }

            // will early-out if we're already in the compressed state.
            CompressAndRelease();

            if (_bytes == null)
                return;

            string data = ObjectSerializer.Base64Encode(_bytes);
            WriteChunks(node, data);
        }
    }

    public class PersistentCompressedCraftNode : PersistentCompressedConfigNode
    {
        private static bool _IsUpgradingCraft = false;
        public static bool IsUpgradingCraft => _IsUpgradingCraft;

        public override ConfigNode Node
        {
            get
            {
                if (_node == null)
                {
                    if (!Decompress())
                        return _node;

                    Version gameVersion = new Version(Versioning.version_major, Versioning.version_minor, Versioning.Revision);
                    _IsUpgradingCraft = true;
                    ConfigNode newNode = KSPUpgradePipeline.Pipeline.Run(_node, SaveUpgradePipeline.LoadContext.Craft, gameVersion, out bool runSuccess, out string runInfo);
                    _IsUpgradingCraft = false;
                    if (!runSuccess)
                    {
                        UnityEngine.Debug.LogError($"[RP-0] Error upgrading craft node with ship name {_node.GetValue("ship") ?? "<unknown"}. Information from the upgrade run: {runInfo}");
                        return _node;
                    }
                    if (newNode != _node)
                    {
                        base.Node = newNode;
                        UnityEngine.Debug.Log($"[RP-0] Upgraded craft node with ship name {_node.GetValue("ship") ?? "<unknown>"}.");
                    }
                }
                return _node;
            }
            set
            {
                base.Node = value;
            }
        }
    }
}
