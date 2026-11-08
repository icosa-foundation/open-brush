// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System.Collections.Generic;

namespace TiltBrush
{
    public sealed class ShapeMaterialParameter
    {
        public enum Property { Unlit, Metallic, Glossiness, Transparent, Opacity }
        public Property Key;
        public string Label;
        public bool Boolean => Key == Property.Unlit || Key == Property.Transparent;
        public float Read(EditableModelMaterialData value) => Key switch
        {
            Property.Unlit => value.Unlit ? 1 : 0,
            Property.Transparent => value.Transparent ? 1 : 0,
            Property.Metallic => value.Metallic,
            Property.Glossiness => value.Smoothness,
            _ => value.Opacity
        };
        public void Write(EditableModelMaterialData value, float number)
        {
            switch (Key)
            {
                case Property.Unlit: value.Unlit = number >= .5f; break;
                case Property.Transparent: value.Transparent = number >= .5f; break;
                case Property.Metallic: value.Metallic = number; break;
                case Property.Glossiness: value.Smoothness = number; break;
                case Property.Opacity: value.Opacity = number; break;
            }
        }
        public static List<ShapeMaterialParameter> For(EditableModelMaterialData value)
        {
            var result = new List<ShapeMaterialParameter> { new ShapeMaterialParameter { Key = Property.Unlit, Label = "Unlit" } };
            if (!value.Unlit)
            {
                result.Add(new ShapeMaterialParameter { Key = Property.Metallic, Label = "Metallic" });
                result.Add(new ShapeMaterialParameter { Key = Property.Glossiness, Label = "Glossiness" });
            }
            result.Add(new ShapeMaterialParameter { Key = Property.Transparent, Label = "Transparent" });
            if (value.Transparent) result.Add(new ShapeMaterialParameter { Key = Property.Opacity, Label = "Opacity" });
            return result;
        }
    }
}
