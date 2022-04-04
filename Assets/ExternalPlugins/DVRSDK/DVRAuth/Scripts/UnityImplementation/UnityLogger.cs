using DVRSDK.Log;
using UnityEngine;

public class UnityLogger : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        DebugLog.LogAction += LogEvent;
    }

    private void LogEvent(LogLevel level, string message)
    {
        switch (level)
        {
            case LogLevel.Info:
                Debug.Log(message);
                break;
            case LogLevel.Warning:
                Debug.LogWarning(message);
                break;
            case LogLevel.Error:
                Debug.LogError(message);
                break;
        }
    }
}
