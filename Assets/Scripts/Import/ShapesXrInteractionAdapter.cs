// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace TiltBrush.DocumentImport
{
    /// Saved interaction metadata for a resolved static view. Reading never executes it.
    public sealed class ShapesXrInteractionData
    {
        public Guid StateId { get; internal set; }
        public Guid Id { get; internal set; }
        public int Order { get; internal set; }
        public int TriggerKind { get; internal set; }
        public int ActionKind { get; internal set; }
        public JToken TriggerData { get; internal set; }
        public JToken ActionData { get; internal set; }
        public string ActionName => ActionKind switch
        {
            0 => "scene navigation", 1 => "space navigation", 2 => "back navigation", 3 => "viewpoint navigation",
            4 => "sound playback", 5 => "look-at", 6 => "haptic feedback", 7 => "state transition",
            _ => $"action {ActionKind}"
        };
    }

    public static class ShapesXrInteractionAdapter
    {
        public static IReadOnlyList<ShapesXrInteractionData> Read(ShapesXrDocument document, ShapesXrScene scene)
        {
            if (document == null) { throw new ArgumentNullException(nameof(document)); }
            if (scene == null) { throw new ArgumentNullException(nameof(scene)); }
            var result = new List<ShapesXrInteractionData>();
            foreach (Guid stateId in scene.ResolvedStateIds)
            {
                var raw = document.Entities[stateId].Properties["st-inr"];
                if (raw == null) { continue; }
                if (!(raw is JArray interactions)) { throw new InvalidDataException("Invalid ShapesXR interactivity list"); }
                foreach (var item in interactions)
                {
                    if (!(item is JArray row) || row.Count < 4 || !(row[2] is JArray trigger) || trigger.Count != 2 ||
                        !(row[3] is JArray action) || action.Count != 2)
                    {
                        throw new InvalidDataException("Invalid ShapesXR interaction record");
                    }
                    result.Add(new ShapesXrInteractionData
                    {
                        StateId = stateId, Id = ShapesXrDocument.ReadGuid(row[0]), Order = Integer(row[1]),
                        TriggerKind = Integer(trigger[0]), ActionKind = Integer(action[0]),
                        TriggerData = trigger[1].DeepClone(), ActionData = action[1].DeepClone()
                    });
                }
            }
            return result.AsReadOnly();
        }

        private static int Integer(JToken value)
        {
            if (value?.Type != JTokenType.Integer) { throw new InvalidDataException("Expected a ShapesXR interaction integer"); }
            return (int)value;
        }
    }
}
