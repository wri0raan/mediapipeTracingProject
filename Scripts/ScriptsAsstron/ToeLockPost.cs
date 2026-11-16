using UnityEngine;

[ExecuteAlways]
[DefaultExecutionOrder(10000)]
public class ToeLockPost : MonoBehaviour
{
    public Animator anim;

    [Header("옵션")]
    public bool followPosition = true;
    public bool autoRebindOnPlay = true;
    [Range(0f, 0.15f)]
    public float clampMaxDist = 0.0f;

    Transform footL, footR, toeL, toeR;

    Vector3 lLocalPos, rLocalPos;
    Quaternion lLocalRot, rLocalRot;

    void Reset() { anim = GetComponent<Animator>(); }
    void OnEnable()
    {
        if (!anim) anim = GetComponent<Animator>();
        FindBones();
        if (autoRebindOnPlay) RebindFromCurrentPose();
    }

    void OnValidate()
    {
        if (!isActiveAndEnabled) return;
        if (!anim) anim = GetComponent<Animator>();
        FindBones();
    }

    void FindBones()
    {
        if (!anim) return;
        footL = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
        footR = anim.GetBoneTransform(HumanBodyBones.RightFoot);

        toeL = FindByNames("toe.L", "Toe.L", "toe_l", "Toe_L", "toePivot.L", "ToePivot.L");
        toeR = FindByNames("toe.R", "Toe.R", "toe_r", "Toe_R", "toePivot.R", "ToePivot.R", "toe.R.001", "Toe_R");

        if (!toeL && footL) toeL = FindChildContains(footL, "toe");
        if (!toeR && footR) toeR = FindChildContains(footR, "toe");
    }

    Transform FindByNames(params string[] names)
    {
        var all = GetComponentsInChildren<Transform>(true);
        foreach (var t in all)
            foreach (var n in names)
                if (t.name == n) return t;
        return null;
    }
    Transform FindChildContains(Transform root, string keywordLower)
    {
        keywordLower = keywordLower.ToLower();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t != root && t.name.ToLower().Contains(keywordLower)) return t;
        return null;
    }

    [ContextMenu("Rebind From Current Pose")]
    public void RebindFromCurrentPose()
    {
        if (footL && toeL)
        {
            lLocalPos = footL.InverseTransformPoint(toeL.position);
            lLocalRot = Quaternion.Inverse(footL.rotation) * toeL.rotation;
        }
        if (footR && toeR)
        {
            rLocalPos = footR.InverseTransformPoint(toeR.position);
            rLocalRot = Quaternion.Inverse(footR.rotation) * toeR.rotation;
        }
    }

    void LateUpdate()
    {

        if (footL && toeL)
        {
            if (followPosition)
            {
                var pos = footL.TransformPoint(lLocalPos);
                if (clampMaxDist > 0f)
                {
                    var v = pos - footL.position;
                    var m = v.magnitude;
                    if (m > clampMaxDist) pos = footL.position + v * (clampMaxDist / (m + 1e-6f));
                }
                toeL.position = pos;
            }
            toeL.rotation = footL.rotation * lLocalRot;
            toeL.localScale = Vector3.one;
        }

        if (footR && toeR)
        {
            if (followPosition)
            {
                var pos = footR.TransformPoint(rLocalPos);
                if (clampMaxDist > 0f)
                {
                    var v = pos - footR.position;
                    var m = v.magnitude;
                    if (m > clampMaxDist) pos = footR.position + v * (clampMaxDist / (m + 1e-6f));
                }
                toeR.position = pos;
            }
            toeR.rotation = footR.rotation * rLocalRot;
            toeR.localScale = Vector3.one;
        }
    }
}
