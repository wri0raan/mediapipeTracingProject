using UnityEngine;

[ExecuteAlways]
[DefaultExecutionOrder(10000)]
public class ToeRigidFollower : MonoBehaviour
{
    public Animator anim;
    public bool followPosition = true;
    [Tooltip("루트 트랜스폼 회전/스케일이 바뀌면 자동으로 Rebind")]
    public bool autoRebindOnRootChange = true;

    Transform footL, footR, toeL, toeR;
    Vector3 lLocalPos, rLocalPos;
    Quaternion lLocalRot, rLocalRot;

    Matrix4x4 lastRootMatrix;

    void Reset() => anim = GetComponent<Animator>();

    void OnEnable()
    {
        if (!anim) anim = GetComponent<Animator>();
        FindBones();
        RebindFromCurrentPose();
    }

    void OnValidate()
    {

        if (isActiveAndEnabled && anim) { FindBones(); RebindFromCurrentPose(); }
    }

    void FindBones()
    {
        if (!anim) return;
        footL = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
        footR = anim.GetBoneTransform(HumanBodyBones.RightFoot);
        toeL = FindByNames("toe.L", "Toe.L", "toe_l", "Toe_L");
        toeR = FindByNames("toe.R", "Toe.R", "toe_r", "Toe_R");
    }

    Transform FindByNames(params string[] names)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
            foreach (var n in names) if (t.name == n) return t;
        return null;
    }

    [ContextMenu("Rebind Now")]
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
        lastRootMatrix = transform.localToWorldMatrix;
    }

    void LateUpdate()
    {

        if (autoRebindOnRootChange)
        {
            var m = transform.localToWorldMatrix;
            if (!MatrixApproximatelyEqual(m, lastRootMatrix))
            {
                RebindFromCurrentPose();
            }
        }

        if (footL && toeL)
        {
            if (followPosition) toeL.position = footL.TransformPoint(lLocalPos);
            toeL.rotation = footL.rotation * lLocalRot;
            toeL.localScale = Vector3.one;
        }
        if (footR && toeR)
        {
            if (followPosition) toeR.position = footR.TransformPoint(rLocalPos);
            toeR.rotation = footR.rotation * rLocalRot;
            toeR.localScale = Vector3.one;
        }
    }

    static bool MatrixApproximatelyEqual(Matrix4x4 a, Matrix4x4 b, float eps = 1e-4f)
    {
        for (int i = 0; i < 16; i++)
            if (Mathf.Abs(a[i] - b[i]) > eps) return false;
        return true;
    }
}
