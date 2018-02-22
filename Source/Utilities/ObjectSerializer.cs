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
	}
}
