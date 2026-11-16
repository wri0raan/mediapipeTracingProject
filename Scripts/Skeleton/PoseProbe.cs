using System.Collections.Generic;
using UnityEngine;

public class PoseProbe : MonoBehaviour
{
    [Header("PoseProvider_FromAnnotation 컴포넌트를 드래그")]
    public MonoBehaviour provider;

    [Header("Logging (레이트 리밋)")]
    [Tooltip("정보 로그 간격(초). 0 이하면 비활성화")]
    public float infoEverySeconds = 1.0f;
    [Tooltip("경고 로그 간격(초). 0 이하면 비활성화")]
    public float warnEverySeconds = 1.0f;
    [Tooltip("빌드(Player)에서는 로그를 끌지 여부")]
    public bool editorOnlyLogs = true;

    [Header("Stats (읽기전용)")]
    [Tooltip("연속 실패 카운트")]
    public int missCount;
    [Tooltip("마지막으로 유효 포즈를 받은 시간(초, unscaled)")]
    public float lastGoodTimeUnscaled;
    [Tooltip("현재 프레임에 포즈를 받았는지")]
    public bool hasPoseThisFrame;

    IPoseProvider _p;

    float _nextInfoLogTime;
    float _nextWarnLogTime;

    void Awake()
    {
        Debug.Log("[Probe] Awake");
        _p = provider as IPoseProvider;
        if (_p == null)
        {
            Debug.LogError("[Probe] provider 슬롯이 비었거나 IPoseProvider가 아님. PoseProvider_FromAnnotation 컴포넌트를 직접 드래그하세요.");
        }
        lastGoodTimeUnscaled = -1f;
        _nextInfoLogTime = 0f;
        _nextWarnLogTime = 0f;
    }

    void Update()
    {
        if (_p == null) return;

        hasPoseThisFrame = false;

        if (_p.TryGetPose(out var uvz) && uvz != null && uvz.Count >= 33)
        {
            hasPoseThisFrame = true;
            missCount = 0;
            lastGoodTimeUnscaled = Time.unscaledTime;

            if (ShouldLogInfo())
            {
                if (infoEverySeconds > 0f && Time.unscaledTime >= _nextInfoLogTime)
                {
                    _nextInfoLogTime = Time.unscaledTime + infoEverySeconds;

                    Debug.Log("[Probe] landmarks=" + uvz.Count + ", nose=" + uvz[0]);
                }
            }
        }
        else
        {
            missCount++;

            if (ShouldLogWarn())
            {
                if (warnEverySeconds > 0f && Time.unscaledTime >= _nextWarnLogTime)
                {
                    _nextWarnLogTime = Time.unscaledTime + warnEverySeconds;
                    Debug.LogWarning("[Probe] TryGetPose=false (누적 " + missCount + "). 러너/오버레이/배선/Animator 충돌 점검 필요.");
                }
            }
        }
    }

    bool ShouldLogInfo()
    {
        if (infoEverySeconds <= 0f) return false;
#if UNITY_EDITOR
        return true;
#else
        return !editorOnlyLogs;
#endif
    }

    bool ShouldLogWarn()
    {
        if (warnEverySeconds <= 0f) return false;
#if UNITY_EDITOR
        return true;
#else
        return !editorOnlyLogs;
#endif
    }
}
