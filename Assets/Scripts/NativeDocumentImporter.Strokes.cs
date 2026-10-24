// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using TiltBrush.DocumentImport;
using UnityEngine;

namespace TiltBrush
{
    public static partial class NativeDocumentImporter
    {
        private static readonly Guid kNativeRibbonBrush = new Guid("c4f8b3e2-9d1a-5e7f-4c3b-8a6d2f9e1c7b");
        private static readonly Guid kNativeLitRibbonBrush = new Guid("fd752c0b-05fe-436f-a1d2-a7c328ad3a1f");
        private static readonly Guid kNativeAlphaRibbonBrush = new Guid("51094f7e-e9ed-4ce0-a74b-d173ba924ec9");
        private static readonly Guid kNativeDiscardingAlphaRibbonBrush = new Guid("c8a23976-d986-4dd0-ae52-d49c64c73ae7");
        private enum NativeRibbonAppearance { Unlit, Lit, AlphaBlend, AlphaBlendDiscard }
        private static readonly Guid kFacetedPointHullBrush = new Guid("d0139741-9d31-43f1-a98d-a388c70b4a63");
        private static readonly Guid kSmoothPointHullBrush = new Guid("fa35383b-64a7-4561-9dc7-cc547a752318");

        private static Stroke CreateNativeHull(NativeHullData hull, CanvasScript canvas, Color color, SketchGroupTag group)
        {
            var brush = BrushCatalog.m_Instance.GetBrush(hull.Faceted ? kFacetedPointHullBrush : kSmoothPointHullBrush);
            if (brush == null) { throw new InvalidOperationException("The point hull brush is unavailable"); }
            uint time = (uint)(App.Instance.CurrentSketchTime * 1000);
            var points = new PointerManager.ControlPoint[hull.Points.Length];
            for (int i = 0; i < points.Length; ++i)
            {
                var p = hull.Points[i];
                points[i] = new PointerManager.ControlPoint { m_Pos = new Vector3(p[0], p[1], p[2]),
                    m_Orient = Quaternion.identity, m_Pressure = 1, m_TimestampMs = time };
            }
            return CreateNativeStroke(points, brush, 1, canvas, color, group);
        }

        private static Stroke CreateNativeRibbon(NativeRibbonData ribbon, CanvasScript canvas, Color color, SketchGroupTag group,
            NativeRibbonAppearance appearance = NativeRibbonAppearance.Unlit)
        {
            Guid brushGuid = appearance switch
            {
                NativeRibbonAppearance.Lit => kNativeLitRibbonBrush,
                NativeRibbonAppearance.AlphaBlend => kNativeAlphaRibbonBrush,
                NativeRibbonAppearance.AlphaBlendDiscard => kNativeDiscardingAlphaRibbonBrush,
                _ => kNativeRibbonBrush
            };
            var brush = BrushCatalog.m_Instance.GetBrush(brushGuid);
            if (brush == null) { throw new InvalidOperationException("The compatible ribbon brush is unavailable"); }
            int count = ribbon.FirstEdge.Length;
            var centers = new Vector3[count];
            var widths = new Vector3[count];
            float maximumWidth = 0;
            Vector3 Vector(float[] value) => new Vector3(value[0], value[1], value[2]);
            for (int i = 0; i < count; ++i)
            {
                Vector3 a = Vector(ribbon.FirstEdge[i]), b = Vector(ribbon.SecondEdge[i]);
                widths[i] = b - a;
                centers[i] = a + widths[i] * .5f;
                maximumWidth = Mathf.Max(maximumWidth, widths[i].magnitude);
            }
            if (!(maximumWidth > 0) || float.IsInfinity(maximumWidth)) { throw new InvalidOperationException("Ribbon has no finite positive width"); }
            var points = new PointerManager.ControlPoint[count];
            Vector3 previousRight = Vector3.right, previousUp = Vector3.up;
            uint time = (uint)(App.Instance.CurrentSketchTime * 1000);
            for (int i = 0; i < count; ++i)
            {
                float width = widths[i].magnitude;
                Vector3 right = width > 0 ? widths[i] / width : previousRight;
                Vector3 tangent = centers[Mathf.Min(i + 1, count - 1)] - centers[Mathf.Max(i - 1, 0)];
                Vector3 up = Vector3.Cross(right, tangent);
                if (up.sqrMagnitude < 1e-12f) { up = previousUp - right * Vector3.Dot(previousUp, right); }
                if (up.sqrMagnitude < 1e-12f) { up = Vector3.Cross(right, Mathf.Abs(right.y) < .9f ? Vector3.up : Vector3.forward); }
                up.Normalize();
                points[i] = new PointerManager.ControlPoint
                {
                    m_Pos = centers[i], m_Orient = Quaternion.LookRotation(Vector3.Cross(right, up), up),
                    m_Pressure = width / maximumWidth, m_TimestampMs = time
                };
                previousRight = right; previousUp = up;
            }
            return CreateNativeStroke(points, brush, maximumWidth, canvas, color, group);
        }

        private static Stroke CreateNativeStroke(PointerManager.ControlPoint[] points, BrushDescriptor brush,
            float size, CanvasScript canvas, Color color, SketchGroupTag group)
        {
            var stroke = new Stroke
            {
                m_Type = Stroke.Type.NotCreated, m_IntendedCanvas = canvas, m_BrushGuid = brush.m_Guid,
                m_BrushScale = 1, m_BrushSize = size, m_Color = color, m_Seed = 0,
                m_ControlPoints = points, m_ControlPointsToDrop = new bool[points.Length]
            };
            stroke.Recreate(canvas: canvas);
            stroke.Group = group;
            SketchMemoryScript.m_Instance.MemoryListAdd(stroke);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(new BrushStrokeCommand(stroke));
            return stroke;
        }
    }
}
