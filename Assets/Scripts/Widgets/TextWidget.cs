// Copyright 2023 The Open Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace TiltBrush
{
    public enum TextWidgetMode
    {
        TextMeshPro,
        Geometry
    }

    public class TextWidget : Media2dWidget
    {
        public TextMeshPro m_TextMeshPro;
        public MeshRenderer m_MeshRenderer;
        public MeshFilter m_MeshFilter;
        private bool m_HasLayout;
        private bool m_ListeningForMesh;
        private Matrix4x4 m_GlyphTransform = Matrix4x4.identity;
        private Bounds m_GlyphBounds;
        private bool m_HasGlyphBounds;
        private string m_FontReference;
        private IDictionary<string, JToken> m_LayoutExtraData;

        public string FontReference => m_FontReference ?? m_TextMeshPro.font.name;
        public bool HasUnresolvedFont { get; private set; }

        public override float? AspectRatio => m_HasLayout
            ? (m_HasGlyphBounds && m_GlyphBounds.size.y > 0 ? m_GlyphBounds.size.x / m_GlyphBounds.size.y : 1)
            : m_TextMeshPro.renderedWidth / m_TextMeshPro.renderedHeight;

        protected override void UpdateScale()
        {
            base.UpdateScale();
            if (m_HasLayout)
            {
                // Media2dWidget stretches images to their aspect ratio. Text already has authored
                // glyph dimensions; its layout matrix carries any intrinsic stretch or shear.
                m_TextMeshPro.transform.localPosition = Vector3.zero;
                m_TextMeshPro.transform.localRotation = Quaternion.identity;
                m_TextMeshPro.transform.localScale = Vector3.one * m_Size * m_TransitionScale;
            }
            UpdateCollider();
        }

        public void AssignFont(TMP_FontAsset fontAsset, string reference = null)
        {
            m_TextMeshPro.font = fontAsset;
            m_FontReference = reference ?? fontAsset.name;
            HasUnresolvedFont = false;
        }

        public void AssignFont(string path)
        {
            var fontAsset = SvgTextUtils.ConvertOpenTypeToTMPro(path);
            m_TextMeshPro.font = fontAsset;
            m_FontReference = path;
            HasUnresolvedFont = false;
        }

        public void AssignFontReference(string reference)
        {
            if (string.IsNullOrEmpty(reference)) { return; }
            m_FontReference = reference;
            var font = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(asset => asset.name == reference)
                       ?? Resources.Load<TMP_FontAsset>(reference);
            if (font != null) { m_TextMeshPro.font = font; HasUnresolvedFont = false; }
            else if (File.Exists(reference)) { AssignFont(reference); }
            else { HasUnresolvedFont = true; }
        }

        public TextWidgetLayout CaptureLayout()
        {
            Matrix4x4 matrix = m_GlyphTransform;
            if (!m_HasLayout)
            {
                // Preserve the existing image-style stretch when saving a legacy text widget.
                float aspect = AspectRatio ?? 1;
                if (float.IsNaN(aspect) || float.IsInfinity(aspect)) { aspect = 1; }
                Vector3 scale = m_BaseScale == Vector3.zero ? Vector3.one : m_BaseScale;
                scale.x *= aspect;
                matrix = Matrix4x4.TRS(m_TextMeshPro.transform.localPosition, m_TextMeshPro.transform.localRotation, scale);
            }
            var components = new float[16];
            for (int i = 0; i < components.Length; ++i) { components[i] = matrix[i]; }
            return new TextWidgetLayout
            {
                FontSize = m_TextMeshPro.fontSize, AutoSize = m_TextMeshPro.enableAutoSizing,
                FontSizeMin = m_TextMeshPro.fontSizeMin, FontSizeMax = m_TextMeshPro.fontSizeMax,
                FontStyle = (int)m_TextMeshPro.fontStyle, Alignment = (int)m_TextMeshPro.alignment,
                Wrapping = (int)m_TextMeshPro.textWrappingMode, RichText = m_TextMeshPro.richText,
                Size = m_TextMeshPro.rectTransform.sizeDelta, Pivot = m_TextMeshPro.rectTransform.pivot,
                GlyphTransform = components,
                ExtraData = m_LayoutExtraData?.ToDictionary(entry => entry.Key, entry => entry.Value.DeepClone())
            };
        }

        public void ApplyLayout(TextWidgetLayout layout)
        {
            if (layout == null) { return; }
            layout.Validate();
            Matrix4x4 matrix = Matrix4x4.identity;
            if (layout.GlyphTransform != null)
            {
                for (int i = 0; i < 16; ++i) { matrix[i] = layout.GlyphTransform[i]; }
            }
            m_GlyphTransform = matrix;
            m_HasLayout = true;
            m_LayoutExtraData = layout.ExtraData;
            if (!m_ListeningForMesh)
            {
                m_TextMeshPro.OnPreRenderText += TransformGlyphs;
                m_ListeningForMesh = true;
            }
            m_TextMeshPro.fontSize = layout.FontSize;
            m_TextMeshPro.fontSizeMin = layout.FontSizeMin;
            m_TextMeshPro.fontSizeMax = layout.FontSizeMax;
            m_TextMeshPro.enableAutoSizing = layout.AutoSize;
            m_TextMeshPro.fontStyle = (TMPro.FontStyles)layout.FontStyle;
            m_TextMeshPro.alignment = (TextAlignmentOptions)layout.Alignment;
            m_TextMeshPro.textWrappingMode = (TextWrappingModes)layout.Wrapping;
            m_TextMeshPro.richText = layout.RichText;
            m_TextMeshPro.rectTransform.sizeDelta = layout.Size;
            m_TextMeshPro.rectTransform.pivot = layout.Pivot;
            UpdateScale();
        }

        private void TransformGlyphs(TMP_TextInfo info)
        {
            m_HasGlyphBounds = false;
            Vector3 normal = m_GlyphTransform.inverse.transpose.MultiplyVector(Vector3.back).normalized;
            if (normal == Vector3.zero)
            {
                normal = -Vector3.Cross(m_GlyphTransform.MultiplyVector(Vector3.right), m_GlyphTransform.MultiplyVector(Vector3.up)).normalized;
            }
            Vector3 tangent = m_GlyphTransform.MultiplyVector(Vector3.left).normalized;
            float handedness = m_GlyphTransform.determinant < 0 ? -1 : 1;
            float glyphScale = m_GlyphTransform.MultiplyVector(Vector3.up).magnitude;
            for (int material = 0; material < info.meshInfo.Length; ++material)
            {
                var mesh = info.meshInfo[material];
                for (int i = 0; i < mesh.vertexCount; ++i)
                {
                    Vector3 position = m_GlyphTransform.MultiplyPoint3x4(mesh.vertices[i]);
                    mesh.vertices[i] = position;
                    // TMP derives its SDF scale from the Transform. Our intrinsic scale is baked
                    // into vertices, so include it in the glyph scale channel uploaded afterward.
                    mesh.uvs0[i].w *= glyphScale;
                    // TMP initializes flat glyph normals once. Start from the canonical normal on
                    // every rebuild so repeated text/layout edits cannot compound the transform.
                    mesh.normals[i] = normal;
                    mesh.tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, handedness);
                    if (!m_HasGlyphBounds) { m_GlyphBounds = new Bounds(position, Vector3.zero); m_HasGlyphBounds = true; }
                    else { m_GlyphBounds.Encapsulate(position); }
                }
                int triangleCount = mesh.vertexCount / 4 * 6;
                for (int i = 0; i < triangleCount; i += 3)
                {
                    int a = mesh.triangles[i], b = mesh.triangles[i + 1], c = mesh.triangles[i + 2];
                    if (Vector3.Dot(Vector3.Cross(mesh.vertices[b] - mesh.vertices[a], mesh.vertices[c] - mesh.vertices[a]), normal) < 0)
                    {
                        mesh.triangles[i + 1] = c;
                        mesh.triangles[i + 2] = b;
                    }
                }
                if (mesh.mesh != null)
                {
                    // TMP uploads positions/UVs/colours after this event, but not these attributes.
                    mesh.mesh.normals = mesh.normals;
                    mesh.mesh.tangents = mesh.tangents;
                    mesh.mesh.triangles = mesh.triangles;
                }
            }
        }

        public override void OnPreDestroy()
        {
            if (m_ListeningForMesh && m_TextMeshPro != null) { m_TextMeshPro.OnPreRenderText -= TransformGlyphs; }
            base.OnPreDestroy();
        }

        public void AssignMesh(Mesh mesh)
        {
            Mode = TextWidgetMode.Geometry;
            m_MeshFilter.mesh = mesh;
        }

        public TextWidgetMode Mode
        {
            get
            {
                return m_TextMeshPro.enabled ?
                    TextWidgetMode.TextMeshPro : TextWidgetMode.Geometry;
            }
            set
            {
                switch (value)
                {
                    case TextWidgetMode.TextMeshPro:
                        m_TextMeshPro.enabled = true;
                        if (m_MeshRenderer) m_MeshRenderer.enabled = false;
                        break;
                    case TextWidgetMode.Geometry: // Not currently working as extrusion support isn't finished
                        m_TextMeshPro.enabled = false;
                        if (m_MeshRenderer) m_MeshRenderer.enabled = true;
                        break;
                }
            }
        }


        public string Text
        {
            get => m_TextMeshPro.text;
            set
            {
                m_TextMeshPro.text = value;
                UpdateCollider();
            }
        }

        private void UpdateCollider()
        {
            // TMP can return before its mesh event for empty text. Do not retain old glyph bounds.
            if (m_HasLayout) { m_HasGlyphBounds = false; }
            m_TextMeshPro.ForceMeshUpdate();
            if (m_HasLayout)
            {
                float widgetScale = m_Size * m_TransitionScale;
                Vector3 extent = m_HasGlyphBounds ? m_GlyphBounds.size * Mathf.Abs(widgetScale) : Vector3.zero;
                m_BoxCollider.transform.localScale = Vector3.one;
                m_BoxCollider.transform.localPosition = m_HasGlyphBounds ? m_GlyphBounds.center * widgetScale : Vector3.zero;
                m_BoxCollider.center = Vector3.zero;
                m_BoxCollider.size = new Vector3(Mathf.Max(.001f, extent.x), Mathf.Max(.001f, extent.y), Mathf.Max(.001f, extent.z));
                return;
            }
            var size = m_TextMeshPro.GetRenderedValues(true);
            // No idea why the 1.3 and 0.9 is necessary, but it is.
            // m_BoxCollider.transform.localScale = new Vector3(size.x * 1.3f, size.y * 0.9f, m_BoxCollider.transform.localScale.z);
            m_BoxCollider.transform.localScale = new Vector3(size.x, size.y, m_BoxCollider.transform.localScale.z);
        }

        public Color TextColor
        {
            get => m_TextMeshPro.color;
            set => m_TextMeshPro.color = value;
        }

        public Color StrokeColor
        {
            get => m_TextMeshPro.outlineColor;
            set => m_TextMeshPro.outlineColor = value;
        }

        public override GrabWidget Clone()
        {
            return Clone(transform.position, transform.rotation, m_Size);
        }

        override public GrabWidget Clone(Vector3 position, Quaternion rotation, float size)
        {
            TextWidget clone = Instantiate(WidgetManager.m_Instance.TextWidgetPrefab);
            clone.m_PreviousCanvas = m_PreviousCanvas;
            clone.transform.position = position;
            clone.transform.rotation = rotation;
            // We're obviously not loading from a sketch.  This is to prevent the intro animation.
            // TODO: Change variable name to something more explicit of what this flag does.
            clone.m_LoadingFromSketch = true;
            // We also want to lie about our intro transition amount.
            // TODO: I think this is an old concept and redundant with other intro anim members.
            clone.m_TransitionScale = 1.0f;
            clone.Show(true, false);
            clone.transform.parent = transform.parent;
            clone.SetSignedWidgetSize(size);
            HierarchyUtils.RecursivelySetLayer(clone.transform, gameObject.layer);
            TiltMeterScript.m_Instance.AdjustMeterWithWidget(clone.GetTiltMeterCost(), up: true);
            clone.CloneInitialMaterials(this);
            clone.m_TextMeshPro.font = m_TextMeshPro.font;
            clone.m_FontReference = m_FontReference;
            clone.HasUnresolvedFont = HasUnresolvedFont;
            clone.Text = Text;
            clone.ApplyLayout(CaptureLayout());
            clone.TextColor = TextColor;
            clone.StrokeColor = StrokeColor;
            clone.Mode = Mode;
            clone.TrySetCanvasKeywordsFromObject(transform);
            return clone;
        }

        public static void FromTiltText(TiltText tiltText)
        {
            TextWidget textWidget = Instantiate(WidgetManager.m_Instance.TextWidgetPrefab);
            textWidget.m_LoadingFromSketch = true;
            textWidget.transform.parent = App.Instance.m_CanvasTransform;
            textWidget.transform.localScale = Vector3.one;
            // Saved coordinates and size are local to the destination layer.
            textWidget.SetCanvas(App.Scene.GetOrCreateLayer(tiltText.LayerId));

            textWidget.Text = tiltText.Text;
            textWidget.SetSignedWidgetSize(tiltText.Transform.scale);
            textWidget.Show(bShow: true, bPlayAudio: false);
            textWidget.transform.localPosition = tiltText.Transform.translation;
            textWidget.transform.localRotation = tiltText.Transform.rotation;
            if (tiltText.Pinned)
            {
                textWidget.PinFromSave();
            }
            textWidget.Text = tiltText.Text;
            textWidget.Mode = tiltText.Mode;
            textWidget.Group = App.GroupManager.GetGroupFromId(tiltText.GroupId);
            if (tiltText.Layout != null)
            {
                // Font was historically a hard-coded label, so only apply it with authored layout.
                textWidget.AssignFontReference(tiltText.Font);
                textWidget.ApplyLayout(tiltText.Layout);
            }
            // Assigning a font replaces its material. Restore authored colors afterward,
            // especially a transparent outline used by imported text.
            textWidget.TextColor = tiltText.FillColor;
            textWidget.StrokeColor = tiltText.StrokeColor;

            TiltMeterScript.m_Instance.AdjustMeterWithWidget(textWidget.GetTiltMeterCost(), up: true);
            textWidget.UpdateScale();
        }

        public override string GetExportName()
        {
            return $"Text: {m_TextMeshPro.text}";
        }

        // TODO
        public void SetExtrusion(float depth, Color color)
        {
            if (depth > 0)
            {
                Mode = TextWidgetMode.Geometry;
            }
            else
            {
                Mode = TextWidgetMode.TextMeshPro;
            }
        }
    }
} // namespace TiltBrush
