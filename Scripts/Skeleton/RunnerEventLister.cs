using UnityEngine;

public class RunnerEventLister : MonoBehaviour
{
    [Header("Only log events, do NOT restart sources")]
    public bool log = true;

    void OnEnable()
    {

    }

    public void OnRunnerStarted()
    {
        if (log) Debug.Log("[Runner] Started");

    }

    public void OnRunnerStopped()
    {
        if (log) Debug.Log("[Runner] Stopped");

    }
}
