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

        public override float? AspectRatio =>
            m_TextMeshPro.renderedWidth / m_TextMeshPro.renderedHeight;

        protected override void UpdateScale()
        {
            base.UpdateScale();
            UpdateCollider();
        }

        public void AssignFont(TMP_FontAsset fontAsset)
        {
            m_TextMeshPro.font = fontAsset;
        }

        public void AssignFont(string path)
        {
            var fontAsset = SvgTextUtils.ConvertOpenTypeToTMPro(path);
            m_TextMeshPro.font = fontAsset;
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
                        m_MeshRenderer.enabled = false;
                        break;
                    case TextWidgetMode.Geometry:
                        m_TextMeshPro.enabled = false;
                        m_MeshRenderer.enabled = true;
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
            m_TextMeshPro.ForceMeshUpdate();
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
            clone.TrySetCanvasKeywordsFromObject(transform);
            return clone;
        }

        public static void FromTiltText(TiltText tiltText)
        {
            TextWidget textWidget = Instantiate(WidgetManager.m_Instance.TextWidgetPrefab);
            textWidget.m_LoadingFromSketch = true;
            textWidget.transform.parent = App.Instance.m_CanvasTransform;
            textWidget.transform.localScale = Vector3.one;

            textWidget.Text = tiltText.Text;
            textWidget.SetSignedWidgetSize(tiltText.Transform.scale);
            textWidget.Show(bShow: true, bPlayAudio: false);
            textWidget.transform.localPosition = tiltText.Transform.translation;
            textWidget.transform.localRotation = tiltText.Transform.rotation;
            if (tiltText.Pinned)
            {
                textWidget.PinFromSave();
            }
            textWidget.TextColor = tiltText.FillColor;
            textWidget.StrokeColor = tiltText.StrokeColor;
            textWidget.Text = tiltText.Text;
            textWidget.Mode = tiltText.Mode;
            textWidget.Group = App.GroupManager.GetGroupFromId(tiltText.GroupId);
            textWidget.SetCanvas(App.Scene.GetOrCreateLayer(tiltText.LayerId));

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
