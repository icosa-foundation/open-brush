// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace TiltBrush.DocumentImport
{
    public sealed class ShapesXrSoundData
    {
        public Guid ResourceId { get; internal set; }
        public float Volume { get; internal set; }
        public bool Spatialize { get; internal set; }
        public Guid EmitterId { get; internal set; }
        public bool Loop { get; internal set; }
        public bool? StopOnSceneSwitch { get; internal set; }
        public bool AtStateStart { get; internal set; }
    }

    public static class ShapesXrSoundAdapter
    {
        public static ShapesXrSoundData Read(ShapesXrInteractionData interaction)
        {
            if (interaction == null) { throw new ArgumentNullException(nameof(interaction)); }
            if (interaction.ActionKind != 4) { throw new ArgumentException("Expected a ShapesXR sound action", nameof(interaction)); }
            if (!(interaction.ActionData is JArray data) || data.Count < 5 || data.Count > 6)
            { throw new InvalidDataException("Invalid ShapesXR sound action payload"); }
            Guid resource = ShapesXrDocument.ReadGuid(data[0]);
            if (resource == Guid.Empty) { throw new InvalidDataException("Missing ShapesXR sound resource"); }
            if (data[1].Type != JTokenType.Integer && data[1].Type != JTokenType.Float)
            { throw new InvalidDataException("Expected a numeric ShapesXR sound volume"); }
            float volume = (float)data[1];
            if (float.IsNaN(volume) || volume < 0 || volume > 1) { throw new InvalidDataException("Invalid ShapesXR sound volume"); }
            return new ShapesXrSoundData
            {
                ResourceId = resource, Volume = volume, Spatialize = Boolean(data[2]),
                EmitterId = ShapesXrDocument.ReadGuid(data[3]), Loop = Boolean(data[4]),
                StopOnSceneSwitch = data.Count == 6 ? Boolean(data[5]) : (bool?)null,
                AtStateStart = interaction.TriggerKind == 5
            };
        }

        private static bool Boolean(JToken value)
        {
            if (value.Type != JTokenType.Boolean) { throw new InvalidDataException("Expected a ShapesXR sound flag"); }
            return (bool)value;
        }
    }
}
