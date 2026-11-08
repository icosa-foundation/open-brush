// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using TiltBrush.DocumentImport;
using TiltBrush.MeshEditing;

namespace TiltBrush
{
    public class ShapeParameterToggle : ValueToggle
    {
        public void Configure(ShapesPanel panel, ProceduralShapes.Parameter parameter, GeometryRecipeData recipe) =>
            Configure(parameter.Label, parameter.Ellipse ? panel.EllipseEnabled : parameter.Read(recipe) != 0,
                value => panel.ChangeParameter(parameter, value ? 1f : 0f), panel.FinishChange);
    }
}
