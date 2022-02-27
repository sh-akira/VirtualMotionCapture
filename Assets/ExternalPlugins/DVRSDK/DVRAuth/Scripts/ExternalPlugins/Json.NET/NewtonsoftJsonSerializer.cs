using Newtonsoft.Json;

namespace DVRSDK.Serializer
{
    public class NewtonsoftJsonSerializer : IJsonSerializer
    {
        public T Deserialize<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }

        public string Serialize(object target)
        {
            return JsonConvert.SerializeObject(target, Formatting.None);
        }
    }
}
