using System;
using Conway;
using UnityEngine;


public class PolyhydraBrushHelper : MonoBehaviour
{

    
    [Space]
    public bool Canonicalize;
    public Vector3 Position = Vector3.zero;
    public Vector3 Rotation = Vector3.zero;
    public Vector3 Scale = Vector3.one;
    public PolyHydraEnums.ColorMethods ColorMethod;
    public Gradient colors;

    [NonSerialized] public ConwayPoly poly;
    

    public Color GetFaceColor(int faceIndex)
    {
        return colors.Evaluate((float)faceIndex / (poly.Faces.Count - 1));
    }

    void OnDrawGizmos () {
        // GizmoHelper.DrawGizmos(poly, transform, true, false, false, false);
    }

    // public void Randomize()
    // {
    //     ShapeType = (ShapeTypes)Random.Range(0, Enum.GetNames(typeof(ShapeTypes)).Length);
    //     JohnsonPolyType = (PolyHydraEnums.JohnsonPolyTypes)Random.Range(0, Enum.GetNames(typeof(PolyHydraEnums.JohnsonPolyTypes)).Length);
    //     PolyType = (PolyTypes)Random.Range(5, Enum.GetNames(typeof(PolyTypes)).Length);
    //     GridType = (PolyHydraEnums.GridTypes)Random.Range(0, Enum.GetNames(typeof(PolyHydraEnums.GridTypes)).Length);
    //     GridShape = (PolyHydraEnums.GridShapes)Random.Range(0, Enum.GetNames(typeof(PolyHydraEnums.GridShapes)).Length);
    //     PrismP = Random.Range(3, 10);
    //     PrismQ = Random.Range(3, 10);
    //     
    // }
}
