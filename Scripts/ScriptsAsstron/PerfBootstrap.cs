using UnityEngine;
using System.Linq;

[DefaultExecutionOrder(-32000)]
public class PerfBootstrap : MonoBehaviour
{
    void Awake()
    {
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 1;
#if !UNITY_EDITOR
        Application.runInBackground = true;

        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.ScriptOnly);
        Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.ScriptOnly);
#endif
    }
}
