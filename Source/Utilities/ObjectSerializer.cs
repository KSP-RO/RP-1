using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace RP0
{
    //Taken from Procedural Parts
    internal static class ObjectSerializer
    {
        internal static byte[] Serialize<T>(T obj)
        {
            MemoryStream stream = new MemoryStream();
            using (stream)
            {
                BinaryFormatter fmt = new BinaryFormatter();
                fmt.Serialize(stream, obj);
            }
            return stream.ToArray();
        }

        internal static T Deserialize<T>(byte[] data)
        {
            using (MemoryStream stream = new MemoryStream(data))
            {
                BinaryFormatter fmt = new BinaryFormatter();
                return (T)fmt.Deserialize(stream);
            }
        }

        internal static string Base64Encode(byte[] bytes)
        {
            return System.Convert.ToBase64String(bytes);
        }

        internal static string Base64EncodeString(string text)
        {
            return Base64Encode(System.Text.Encoding.UTF8.GetBytes(text));
        }

        internal static byte[] Base64Decode(string base64EncodedData)
        {
            return System.Convert.FromBase64String(base64EncodedData);
        }

        internal static string Base64DecodeString(string base64Text)
        {
            return System.Text.Encoding.UTF8.GetString(Base64Decode(base64Text));
        }

        internal static byte[] Zip(string text)
        {
            if (text == null)
                return null;

            byte[] input = System.Text.Encoding.UTF8.GetBytes(text);

            using (Stream memOutput = new MemoryStream())
            {
                var zip = new Ionic.Zip.ZipFile();
                zip.CompressionLevel = Ionic.Zlib.CompressionLevel.BestCompression;
                zip.AddEntry("a", input);
                zip.Save(memOutput);

                byte[] bytes = new byte[memOutput.Length];
                memOutput.Seek(0, SeekOrigin.Begin);
                memOutput.Read(bytes, 0, bytes.Length);

                return bytes;
            }
        }

        internal static string UnZip(byte[] bytes)
        {
            if (bytes == null)
                return null;

            using (Stream memInput = new MemoryStream(bytes))
            {
                using (var zipStream = new Ionic.Zip.ZipInputStream(memInput))
                {
                    using (StreamReader reader = new StreamReader(zipStream))
                    {
                        zipStream.GetNextEntry();
                        return reader.ReadToEnd();
                    }
                }
            }
        }
    }
}
