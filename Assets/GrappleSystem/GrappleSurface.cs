using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrappleSurface : MonoBehaviour
{
    public enum ESurfaceBehaviour
    {
        CanGrapple,
        TimedGrapple,
        CannotGrapple
    }

    [SerializeField] protected ESurfaceBehaviour SurfaceBehaviour;
    [field: SerializeField] public float TimeLimit { get; protected set; } = 10.0f;

    public bool CanGrapple => SurfaceBehaviour != ESurfaceBehaviour.CannotGrapple;
    public bool HasTimeLimit => SurfaceBehaviour == ESurfaceBehaviour.TimedGrapple;
}
