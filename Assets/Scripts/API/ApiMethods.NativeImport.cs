// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using Newtonsoft.Json;
using UnityEngine;

namespace TiltBrush
{
    public static partial class ApiMethods
    {
        [ApiEndpoint("import.maquette", "Import a Maquette project's current scene; returns imported counts and unsupported content", "project.mqp")]
        public static string ImportMaquette(string path)
        {
            var report = NativeDocumentImporter.ImportMaquette(path);
            string json = JsonConvert.SerializeObject(report);
            Debug.Log($"[OB_NATIVE_IMPORT] Maquette: procedural={report.ProceduralObjects}, text={report.TextObjects}, hidden={report.HiddenObjects}, failed={report.FailedObjects}");
            return json;
        }
    }
}
