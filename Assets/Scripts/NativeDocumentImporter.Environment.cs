// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using TiltBrush.DocumentImport;
using UnityEngine;

namespace TiltBrush
{
    public static partial class NativeDocumentImporter
    {
        private static void ImportMaquetteEnvironment(MaquetteDocument document, MaquetteScene scene,
            CanvasScript canvas, NativeDocumentImportReport report)
        {
            ImportMaquetteFog(document, scene, canvas, report);
            try
            {
                var environment = MaquetteEnvironmentAdapter.Read(document, scene);
                if (environment == null) { return; }
                // These source settings are not represented by importing the sun. Keep them
                // visible in the result rather than claiming an environment match.
                foreach (string feature in new[] { "Maquette background", "Maquette floor", "Maquette ambient and reflections" })
                {
                    report.UnmappedEnvironment[feature] = 1;
                }
                var mode = environment.Shadows ? LightMode.Shadow : LightMode.NoShadow;
                var target = App.Scene.GetLight((int)mode);
                if (target.type != LightType.Directional || !target.enabled || target.intensity != 1)
                {
                    throw new InvalidOperationException("The current Open Brush environment has no usable directional sun slot");
                }
                var rgba = environment.SunColor;
                float multiplier = environment.SunIntensity;
                var color = new Color(rgba[0] * multiplier, rgba[1] * multiplier, rgba[2] * multiplier, 1);
                if (float.IsNaN(color.r) || float.IsInfinity(color.r) || float.IsNaN(color.g) || float.IsInfinity(color.g) ||
                    float.IsNaN(color.b) || float.IsInfinity(color.b)) { throw new InvalidOperationException("Sun radiance exceeds supported range"); }
                var q = environment.SunRotation;
                var worldRotation = canvas.Pose.rotation * new Quaternion(q[0], q[1], q[2], q[3]);
                var parentRotation = target.transform.parent == null ? Quaternion.identity : target.transform.parent.rotation;
                var rotation = Quaternion.Inverse(parentRotation) * worldRotation;
                // Existing environment-light metadata stores HDR color and rotation. Folding
                // intensity into color reuses that representation and survives .tilt reload.
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(new ModifyLightCommand(mode, color, rotation, final: true));
                var other = mode == LightMode.Shadow ? LightMode.NoShadow : LightMode.Shadow;
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(new ModifyLightCommand(other, Color.black,
                    App.Scene.GetLight((int)other).transform.localRotation, final: true));
                if (environment.Shadows) { report.UnmappedEnvironment["Maquette soft sun shadows"] = 1; }
            }
            catch (Exception exception)
            {
                report.UnmappedEnvironment["Maquette sun"] = 1;
                report.Detail($"Maquette environment: {exception.Message}");
            }
        }

        private static void ImportMaquetteFog(MaquetteDocument document, MaquetteScene scene,
            CanvasScript canvas, NativeDocumentImportReport report)
        {
            try
            {
                var fog = MaquetteEnvironmentAdapter.ReadFog(document, scene);
                if (fog == null) { return; }
                if (SceneSettings.m_Instance.IsTransitioning || RenderSettings.fogMode != FogMode.Exponential ||
                    (fog.Enabled && fog.Density > 0 && !RenderSettings.fog))
                {
                    throw new InvalidOperationException("The current Open Brush backdrop does not provide active exponential fog");
                }
                float relativeScale = Mathf.Abs(canvas.Pose.scale / App.Scene.Pose.scale);
                float density = fog.ToCanvasDensity(App.METERS_TO_UNITS, relativeScale);
                var rgba = fog.Color;
                var sourceColor = new Color(rgba[0], rgba[1], rgba[2], rgba[3]);
                // Existing custom-environment metadata stores Color32. Quantize now so a
                // reload keeps the imported result, and report any precision loss explicitly.
                Color color = (Color32)sourceColor;
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(new ModifyFogCommand(color, density, final: true));
                if (!fog.Enabled && fog.Density > 0)
                {
                    report.UnmappedEnvironment["Maquette disabled fog density"] = 1;
                }
                if (Mathf.Abs(color.r - sourceColor.r) > 1e-6f || Mathf.Abs(color.g - sourceColor.g) > 1e-6f || Mathf.Abs(color.b - sourceColor.b) > 1e-6f)
                {
                    report.UnmappedEnvironment["Maquette fog color precision"] = 1;
                }
            }
            catch (Exception exception)
            {
                report.UnmappedEnvironment["Maquette fog"] = 1;
                report.Detail($"Maquette fog: {exception.Message}");
            }
        }
    }
}
