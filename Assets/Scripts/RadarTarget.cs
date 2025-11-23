using System.Collections.Generic;
using UnityEngine;

public enum RadarTargetType
{
    Player,
    Ship,
    Other
}

public class RadarTarget : MonoBehaviour
{
    public RadarTargetType type = RadarTargetType.Other;

    public static readonly List<RadarTarget> AllTargets = new List<RadarTarget>();

    private void OnEnable()
    {
        AllTargets.Add(this);
    }

    private void OnDisable()
    {
        AllTargets.Remove(this);
    }
}
