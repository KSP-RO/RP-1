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
        
        private string _name = null;
        private string _id = null;
        private string _comment = null;
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
                    _node.name = _name;
                    _node.id = _id;
                    _node.comment = _comment;
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
            _name = _node.name;
            _id = _node.id;
            _comment = _node.comment;
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
            ret._name = _name;
            ret._id = _id;
            ret._comment = _comment;
            if (_node != null)
                ret._node = _node.CreateCopy();
            else if (_bytes != null)
                ret._bytes = _bytes;

            return ret;
        }

        public virtual void Load(ConfigNode node)
        {
            if (node.values.Count == 0)
                return;

            _name = null;
            _id = null;
            _comment = null;
            var sb = new StringBuilder(_ChunkSize);
            for (int i = 0; i < node._values.Count; ++i)
            {
                var v = node._values[i];
                switch (v.name)
                {
                    case "name":
                        _name = v.value;
                        break;
                    case "id":
                        _id = v.value;
                        break;
                    case "comment":
                        _comment = v.value;
                        break;
                    default:
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
            // will early-out if we're already in the compressed state.
            CompressAndRelease();
            if (_name != null)
                node.AddValue("name", _name);
            if (_id != null)
                node.AddValue("id", _id);
            if (_comment != null)
                node.AddValue("comment", _comment);

            string data = ObjectSerializer.Base64Encode(_bytes);
            WriteChunks(node, data);
        }
    }
}
