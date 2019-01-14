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
        public static object Deserialize(byte[] data, Type type)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = XmlDictionaryReader.CreateBinaryReader(ms, null, new XmlDictionaryReaderQuotas() { MaxArrayLength = int.MaxValue }))
            {
                DataContractSerializer serializer = new DataContractSerializer(type);
                return serializer.ReadObject(reader);
            }
        }

        public static byte[] Serialize(object target)
        {
            using (var ms = new MemoryStream())
            using (var writer = XmlDictionaryWriter.CreateBinaryWriter(ms))
            {
                DataContractSerializer serializer = new DataContractSerializer(target.GetType());
                serializer.WriteObject(writer, target);
                writer.Flush();
                return ms.ToArray();
            }
        }
    }
}
