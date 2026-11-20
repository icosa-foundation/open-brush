// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.IO;
using System.Threading.Tasks;
using TiltBrush.DocumentImport;
using UnityEngine;

namespace TiltBrush
{
    public static partial class NativeDocumentImporter
    {
        private static async Task ImportShapesXrInteractions(ShapesXrDocument document, ShapesXrScene scene,
            CanvasScript canvas, NativeShapesXrAssets assets, NativeDocumentImportReport report)
        {
            System.Collections.Generic.IReadOnlyList<ShapesXrInteractionData> interactions;
            try { interactions = ShapesXrInteractionAdapter.Read(document, scene); }
            catch (Exception exception)
            {
                UnsupportedNativeAction(report, "ShapesXR malformed interaction data");
                report.Detail($"ShapesXR interactions: {exception.Message}");
                return;
            }
            var audio = new NativeAudioCache();
            foreach (var interaction in interactions)
            {
                if (interaction.ActionKind != 4)
                { UnsupportedNativeAction(report, $"ShapesXR {interaction.ActionName}"); continue; }
                try
                {
                    var sound = ShapesXrSoundAdapter.Read(interaction);
                    if (!sound.AtStateStart)
                    { UnsupportedNativeAction(report, "ShapesXR triggered sound playback"); continue; }
                    if (sound.Spatialize)
                    { UnsupportedNativeAction(report, "ShapesXR spatial sound playback"); continue; }
                    var resource = await assets.Get(sound.ResourceId);
                    if (resource.Data.Kind != ShapesXrResourceKind.Audio)
                    { throw new InvalidDataException("ShapesXR sound action references a non-audio resource"); }
                    if (canvas == null) { throw new InvalidOperationException("The import canvas was removed while loading audio"); }
                    var clip = audio.Get(resource.Resource);
                    var command = new CreateWidgetCommand(WidgetManager.m_Instance.SoundClipWidgetPrefab,
                        canvas.Pose, forceTransform: true, skipIntroAnimation: true);
                    SketchMemoryScript.m_Instance.PerformAndRecordCommand(command);
                    var widget = (SoundClipWidget)command.Widget;
                    widget.SetAudioProperties(sound.Volume, sound.Loop, spatialBlend: 0);
                    widget.SetSoundClip(clip);
                    if (widget.Canvas != canvas) { widget.SetCanvas(canvas); }
                    command.SetWidgetCost(widget.GetTiltMeterCost());
                    float deadline = Time.realtimeSinceStartup + 30;
                    while (widget != null && canvas != null && !widget.SoundClipController.Initialized && clip.Error == null)
                    {
                        if (Time.realtimeSinceStartup > deadline) { throw new TimeoutException("Native audio decoding timed out"); }
                        await Task.Yield();
                    }
                    if (widget == null || canvas == null) { throw new InvalidOperationException("The imported audio widget was removed while loading"); }
                    if (clip.Error != null) { throw new IOException(clip.Error); }
                    report.AudioObjects++;
                    if (sound.StopOnSceneSwitch == false)
                    { UnsupportedNativeAction(report, "ShapesXR sound scene-switch lifetime"); }
                }
                catch (Exception exception)
                {
                    report.FailedObjects++;
                    report.Detail($"ShapesXR sound {interaction.Id}: {exception.Message}");
                }
            }
        }

        private static void UnsupportedNativeAction(NativeDocumentImportReport report, string feature)
        {
            report.UnsupportedActions.TryGetValue(feature, out int count);
            report.UnsupportedActions[feature] = count + 1;
        }
    }
}
