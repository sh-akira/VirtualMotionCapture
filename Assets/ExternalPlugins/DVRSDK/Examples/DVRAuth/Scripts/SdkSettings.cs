using UnityEngine;

namespace DVRSDK.Utilities
{
    [CreateAssetMenu(menuName = "DVRSDK/Create Configuration")]
    public class SdkSettings : ScriptableObject
    {
        public string client_id;
    }
}
