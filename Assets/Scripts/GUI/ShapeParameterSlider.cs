// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using TiltBrush.DocumentImport;
using TiltBrush.MeshEditing;

namespace TiltBrush
{
    public class ShapeParameterSlider : ValueSlider
    {
        public void Configure(ShapesPanel panel, ProceduralShapes.Parameter parameter, GeometryRecipeData recipe) =>
            Configure(parameter.Label, parameter.Read(recipe), value => panel.ChangeParameter(parameter, value),
                panel.FinishChange, parameter.Min, parameter.Max, parameter.Integer, parameter.Format, 3);
    }
}
