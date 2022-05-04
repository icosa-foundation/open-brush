using System;
using Polyhydra.Core;
using UnityEngine;


public class PolyhydraBrushHelper : MonoBehaviour
{

    [Space]
    public bool Canonicalize;
    public Vector3 Position = Vector3.zero;
    public Vector3 Rotation = Vector3.zero;
    public Vector3 Scale = Vector3.one;
    public ColorMethods ColorMethod;
    public Gradient colors;

    [NonSerialized] public PolyMesh poly;

    public Color GetFaceColor(int faceIndex)
    {
        return colors.Evaluate((float)faceIndex / (poly.Faces.Count - 1));
    }

}
