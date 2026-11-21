// Copyright 2026 The Open Brush Authors.
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace TiltBrush
{
    internal static class NativeImmStrings
    {
        private sealed class Text
        {
            internal readonly LocalizedString Reference;
            internal string Value;

            internal Text(string key, string fallback)
            {
                Value = fallback;
                Reference = new LocalizedString("Strings", $"NATIVE_IMM_{key}");
                Reference.StringChanged += OnChanged;
            }

            private void OnChanged(string value) => Value = value;
            internal void Dispose() => Reference.StringChanged -= OnChanged;
        }

        // Chapter and viewpoint captions are read each frame. Keep the translated templates
        // subscribed to locale changes instead of starting a localization operation each frame.
        private static readonly Dictionary<string, Text> s_Text = new Dictionary<string, Text>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            foreach (var text in s_Text.Values) text.Dispose();
            s_Text.Clear();
        }

        internal static string Get(string key, string fallback)
        {
            if (!s_Text.TryGetValue(key, out var text))
            {
                text = new Text(key, fallback);
                s_Text.Add(key, text);
            }
            return text.Value;
        }

        internal static string Chapter(int chapter, int count) =>
            string.Format(Get("CHAPTER", "Chapter {0} / {1}"), chapter, count);

        internal static string Viewpoint(int index, int count) =>
            string.Format(Get("VIEWPOINT", "Viewpoint {0} / {1}"),
                index < 0 ? "—" : (index + 1).ToString(), count);
    }
}
