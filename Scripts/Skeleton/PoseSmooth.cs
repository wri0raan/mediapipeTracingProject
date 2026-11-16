using UnityEngine;

public static class PoseSmooth
{

    public static float Alpha(float cutoffHz, float dt)
    {
        if (cutoffHz <= 0f) return 1f;
        var r = 2f * Mathf.PI * cutoffHz * dt;
        return 1f - Mathf.Exp(-r);
    }

    public static Vector3 Exp(Vector3 prev, Vector3 curr, float cutoffHz, float dt)
    {
        float a = Alpha(cutoffHz, dt);
        return Vector3.LerpUnclamped(prev, curr, a);
    }

    public static Quaternion Exp(Quaternion prev, Quaternion curr, float cutoffHz, float dt)
    {
        float a = Alpha(cutoffHz, dt);
        return Quaternion.SlerpUnclamped(prev, curr, a);
    }
}
