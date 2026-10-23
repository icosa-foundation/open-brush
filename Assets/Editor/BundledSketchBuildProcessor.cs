// Copyright 2026 The Open Brush Authors. Licensed under the Apache License, Version 2.0.
using UnityEditor;
using UnityEditor.Build;

public sealed class BundledSketchBuildProcessor : BuildPlayerProcessor
{
    public override void PrepareForBuild(BuildPlayerContext context)
    {
#if OPEN_BRUSH_VIEWER
        if (context.BuildPlayerOptions.target == BuildTarget.Android)
        {
            BundledSketchBuildData.AddToBuild(context);
            UnityEngine.Debug.Log("[OB_BUNDLED_SKETCH] Included packaged examples without moving source assets.");
        }
#endif
    }
}
