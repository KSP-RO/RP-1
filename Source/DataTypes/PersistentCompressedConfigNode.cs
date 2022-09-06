using System;
using System.Collections.Generic;
using UniLinq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace RP0.DataTypes
{
    public class PersistentCompressedConfigNode : IConfigNode
    {
        const int _ChunkSize = 32720;
        const string _ValueName = "serialized";
        
        private byte[] _bytes = null;
        private ConfigNode _node = null;
        public ConfigNode Node
        {
            get
            {
                if (_node == null)
                {
                    if (_bytes == null)
                        return null;

                    //UnityEngine.Debug.Log("@@Extracting Shipnode!! Stack: " + Environment.StackTrace);
                    string s = ObjectSerializer.UnZip(_bytes);
                    _node = ConfigNode.Parse(s);
                    if (_node.nodes.Count > 0)
                        _node = _node.nodes[0]; // this is because when we parse, we wrap in a root node.
                    //UnityEngine.Debug.Log("Resulting node:\n" + _node.ToString());
                }

                return _node;
            }

            set
            {
                _bytes = null;
                _node = value;
            }
        }

        public PersistentCompressedConfigNode() { }

        public PersistentCompressedConfigNode(ConfigNode node, bool compress)
        {
            _node = node;
            if (compress)
                CompressAndRelease();
        }

        private void Compress()
        {
            _bytes = ObjectSerializer.Zip(_node.ToString());
        }

        public void CompressAndRelease()
        {
            if (_node == null)
                return;

            Compress();
            _node = null;
        }

        private unsafe void WriteChunks(ConfigNode node, string data)
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

        private unsafe void LoadData(string s)
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
            _bytes = ObjectSerializer.Base64Decode(s);
        }

        public PersistentCompressedConfigNode CreateCopy()
        {
            var ret = new PersistentCompressedConfigNode();
            if (_node != null)
                ret._node = _node.CreateCopy();
            if (_bytes != null)
                ret._bytes = _bytes;

            return ret;
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
                //switch (v.name)
                //{
                //    default:
                        sb.Append(v.value);
                //        break;
                //}
            }
            if (sb.Length == 0)
                return;
            
            string s = sb.ToString();
            sb.Clear();
            LoadData(s);
        }

        public void Save(ConfigNode node)
        {
            // will early-out if we're already in the compressed state.
            CompressAndRelease();

            string data = ObjectSerializer.Base64Encode(_bytes);
            WriteChunks(node, data);
        }
    }
}
