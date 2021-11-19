using System.Collections.Generic;
using System.Linq;
using TiltBrush;
using UnityEditor;
using UnityEngine;


public class BrushTagger
{
    [MenuItem("Tilt/Rewrite Brush Tags")]
    private static void TagBrushes()
    {
        var whiteboardBrushes = new List<string>
        {
            "Marker",
            "TaperedMarker",
            "SoftHighlighter",
            "CelVinyl",
            "Dots",
            "Icing",
            "Toon",
            "Wire",
            "MatteHull",
            "ShinyHull",
            "UnlitHull",
        };

        TiltBrushManifest brushManifest = AssetDatabase.LoadAssetAtPath<TiltBrushManifest>("Assets/Manifest.asset");
        TiltBrushManifest brushManifestX = AssetDatabase.LoadAssetAtPath<TiltBrushManifest>("Assets/Manifest_Experimental.asset");

        var guids = AssetDatabase.FindAssets("t:BrushDescriptor");
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BrushDescriptor brush = AssetDatabase.LoadAssetAtPath<BrushDescriptor>(path);

            brush.m_Tags = new List<string>();

            EditorUtility.SetDirty(brush);

            if (whiteboardBrushes.Contains(brush.DurableName)) brush.m_Tags.Add("whiteboard");
            if (brushManifest.Brushes.Contains(brush)) brush.m_Tags.Add("default");
            if (brushManifestX.Brushes.Contains(brush)) brush.m_Tags.Add("experimental");
            if (brush.m_AudioReactive) brush.m_Tags.Add("audioreactive");

            if (brush.m_BrushPrefab == null) continue;
            if (brush.m_BrushPrefab.GetComponent<HullBrush>() != null) brush.m_Tags.Add("hull");
            if (brush.m_BrushPrefab.GetComponent<GeniusParticlesBrush>() != null) brush.m_Tags.Add("particle");
            if (brush.m_BrushPrefab.GetComponent<ParentBrush>() != null) brush.m_Tags.Add("broken");
        }
    }
}
