using UnityEngine;

public class AttachToHumanoidBone : MonoBehaviour
{
    public Animator animator;
    public HumanBodyBones targetBone = HumanBodyBones.Head;
    public Vector3 localPositionOffset;
    public Vector3 localEulerOffset;

    void Start()
    {
        if (!animator) animator = GetComponentInParent<Animator>();
        var bone = animator ? animator.GetBoneTransform(targetBone) : null;
        if (!bone) { Debug.LogWarning("[AttachToHumanoidBone] target bone not found"); return; }

        transform.SetParent(bone, worldPositionStays: false);
        transform.localPosition = localPositionOffset;
        transform.localRotation = Quaternion.Euler(localEulerOffset);
    }
}
