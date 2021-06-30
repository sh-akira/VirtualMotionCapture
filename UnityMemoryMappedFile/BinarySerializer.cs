using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace UnityMemoryMappedFile
{
    public class BinarySerializer
    {
        private static Dictionary<Type, DataContractSerializer> serializerCache = new Dictionary<Type, DataContractSerializer>();

        public static object Deserialize(byte[] data, Type type)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = XmlDictionaryReader.CreateBinaryReader(ms, null, new XmlDictionaryReaderQuotas() { MaxArrayLength = int.MaxValue }))
            {
                DataContractSerializer serializer;
                if (serializerCache.TryGetValue(type, out serializer) == false)
                {
                    serializer = new DataContractSerializer(type);
                    serializerCache[type] = serializer;
                }
                return serializer.ReadObject(reader);
            }
        }

        public static byte[] Serialize(object target)
        {
            using (var ms = new MemoryStream())
            using (var writer = XmlDictionaryWriter.CreateBinaryWriter(ms))
            {
                var type = target.GetType();
                DataContractSerializer serializer;
                if (serializerCache.TryGetValue(type, out serializer) == false)
                {
                    serializer = new DataContractSerializer(type);
                    serializerCache[type] = serializer;
                }
                serializer.WriteObject(writer, target);
                writer.Flush();
                return ms.ToArray();
            }
        }
    }
}
