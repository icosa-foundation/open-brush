// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;

namespace TiltBrush
{
    public sealed class PbrMaterialParameter
    {
        public enum Property { Unlit, Metallic, Glossiness, Transparent, Opacity }
        [Flags]
        public enum Controls { Unlit = 1, Metallic = 2, Glossiness = 4, Transparent = 8, Opacity = 16, All = 31 }
        public Property Key;
        public string Label;
        public bool Boolean => Key == Property.Unlit || Key == Property.Transparent;
        public float Read(PbrMaterialValues value) => Key switch
        {
            Property.Unlit => value.Unlit ? 1 : 0,
            Property.Transparent => value.Transparent ? 1 : 0,
            Property.Metallic => value.Metallic,
            Property.Glossiness => value.Smoothness,
            _ => value.Opacity
        };
        public void Write(PbrMaterialValues value, float number)
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
        public void Bind(ValueSlider slider, ValueToggle toggle, PbrMaterialValues values,
            Action<float> changed, Action finished)
        {
            slider.gameObject.SetActive(!Boolean);
            toggle.gameObject.SetActive(Boolean);
            if (Boolean) toggle.Configure(Label, Read(values) != 0, value => changed(value ? 1f : 0f), finished);
            else slider.Configure(Label, Read(values), changed, finished);
        }

        public static List<PbrMaterialParameter> For(PbrMaterialValues value, Controls controls = Controls.All)
        {
            var result = new List<PbrMaterialParameter> { new PbrMaterialParameter { Key = Property.Unlit, Label = "Unlit" } };
            if (!value.Unlit)
            {
                result.Add(new PbrMaterialParameter { Key = Property.Metallic, Label = "Metallic" });
                result.Add(new PbrMaterialParameter { Key = Property.Glossiness, Label = "Glossiness" });
            }
            result.Add(new PbrMaterialParameter { Key = Property.Transparent, Label = "Transparent" });
            if (value.Transparent) result.Add(new PbrMaterialParameter { Key = Property.Opacity, Label = "Opacity" });
            result.RemoveAll(parameter => (controls & (Controls)(1 << (int)parameter.Key)) == 0);
            return result;
        }
    }
}
