using System.Collections.Generic;
using UnityEngine;

public interface IPoseProvider
{

    bool TryGetPose(out List<Vector3> uvz);
}
