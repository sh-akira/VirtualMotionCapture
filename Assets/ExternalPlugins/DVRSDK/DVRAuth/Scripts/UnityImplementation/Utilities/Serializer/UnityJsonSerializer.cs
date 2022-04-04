using System;
using UnityEngine;

namespace DVRSDK.Serializer
{
    public class UnityJsonSerializer : IJsonSerializer
    {
        public T Deserialize<T>(string json)
        {
            return JsonUtility.FromJson<T>(json);
        }

        public string Serialize(object target)
        {
            return JsonUtility.ToJson(target);
        }
    }
}
