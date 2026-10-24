// Copyright 2020 The Tilt Brush Authors
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

using UnityEngine;
using System.IO;
using Unity.VectorGraphics.OpenBrush;

namespace TiltBrush
{
    public class ImageWidget : Media2dWidget
    {
        private static T Unused<T>(T value)
        {
            Debug.LogError("Supposedly-unused value is being used");
            return value;
        }

        [SerializeField] private float m_VertCountScalar = 1;

        private bool m_UseLegacyTint;
        private float m_Opacity = 1.0f;
        private ReferenceImage m_ReferenceImage;
        private bool m_TextureAcquired;
        private bool m_PreserveCustomSize;
        private Color m_Tint = Color.white;
        private ImageBlendMode m_BlendMode;
        private float[] m_GeometryTransform;
        private Mesh m_OriginalPlaneMesh;
        private Mesh m_OwnedPlaneMesh;

        /// RGB multiplier. Alpha remains controlled by Opacity.
        public Color TintColor
        {
            get => m_Tint;
            set
            {
                m_Tint = new Color(value.r, value.g, value.b, 1);
                m_ImageQuad.material.color = new Color(value.r, value.g, value.b, m_Opacity);
            }
        }

        public ImageWidgetLayout CaptureLayout()
        {
            bool tinted = m_Tint != Color.white;
            if (m_GeometryTransform == null && !tinted && m_BlendMode == ImageBlendMode.Cutout) { return null; }
            return new ImageWidgetLayout
            {
                Transform = m_GeometryTransform == null ? null : (float[])m_GeometryTransform.Clone(),
                Tint = tinted ? new[] { m_Tint.r, m_Tint.g, m_Tint.b } : null,
                BlendMode = m_BlendMode
            };
        }

        public void ApplyLayout(ImageWidgetLayout layout)
        {
            layout?.Validate();
            m_GeometryTransform = layout?.Transform == null ? null : (float[])layout.Transform.Clone();
            var tint = layout?.Tint;
            TintColor = tint == null ? Color.white : new Color(tint[0], tint[1], tint[2], 1);
            SetBlendMode(layout?.BlendMode ?? ImageBlendMode.Cutout);
            var filter = m_ImageQuad.GetComponent<MeshFilter>();
            if (m_OriginalPlaneMesh == null) { m_OriginalPlaneMesh = filter.sharedMesh; }
            Mesh previous = m_OwnedPlaneMesh;
            m_OwnedPlaneMesh = null;
            if (m_GeometryTransform != null)
            {
                var matrix = Matrix4x4.identity;
                for (int i = 0; i < 16; ++i) { matrix[i] = m_GeometryTransform[i]; }
                m_OwnedPlaneMesh = Instantiate(m_OriginalPlaneMesh);
                m_OwnedPlaneMesh.name = "Image geometry";
                var vertices = m_OwnedPlaneMesh.vertices;
                var normals = m_OwnedPlaneMesh.normals;
                var tangents = m_OwnedPlaneMesh.tangents;
                var normalMatrix = matrix.inverse.transpose;
                Vector3 fallback = -Vector3.Cross(matrix.MultiplyVector(Vector3.right), matrix.MultiplyVector(Vector3.up)).normalized;
                for (int i = 0; i < vertices.Length; ++i)
                {
                    vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
                    if (i < normals.Length)
                    {
                        var normal = normalMatrix.MultiplyVector(normals[i]).normalized;
                        normals[i] = normal == Vector3.zero ? fallback : normal;
                    }
                    if (i < tangents.Length)
                    {
                        var direction = matrix.MultiplyVector(tangents[i]).normalized;
                        tangents[i] = new Vector4(direction.x, direction.y, direction.z, tangents[i].w * (matrix.determinant < 0 ? -1 : 1));
                    }
                }
                var triangles = m_OwnedPlaneMesh.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    Vector3 normal = a < normals.Length ? normals[a] : fallback;
                    if (Vector3.Dot(Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]), normal) < 0)
                    {
                        triangles[i + 1] = c;
                        triangles[i + 2] = b;
                    }
                }
                m_OwnedPlaneMesh.vertices = vertices;
                m_OwnedPlaneMesh.normals = normals;
                m_OwnedPlaneMesh.tangents = tangents;
                m_OwnedPlaneMesh.triangles = triangles;
                m_OwnedPlaneMesh.RecalculateBounds();
            }
            filter.sharedMesh = m_OwnedPlaneMesh != null ? m_OwnedPlaneMesh : m_OriginalPlaneMesh;
            if (m_SnapGhost != null)
            {
                var ghost = m_SnapGhost.GetComponent<MeshFilter>();
                if (ghost != null) { ghost.sharedMesh = filter.sharedMesh; }
            }
            if (previous != null) { Destroy(previous); }
            m_BoxCollider.transform.localPosition = Vector3.zero;
            m_BoxCollider.center = Vector3.zero;
            UpdateScale();
        }

        private void SetBlendMode(ImageBlendMode mode)
        {
            m_BlendMode = mode;
            var material = m_ImageQuad.material;
            bool transparent = mode == ImageBlendMode.AlphaBlend || mode == ImageBlendMode.Additive;
            material.SetFloat("_ImageBlendMode", (int)mode);
            material.SetInt("_SrcBlend", (int)(transparent ? UnityEngine.Rendering.BlendMode.SrcAlpha : UnityEngine.Rendering.BlendMode.One));
            material.SetInt("_DstBlend", (int)(mode == ImageBlendMode.Additive ? UnityEngine.Rendering.BlendMode.One :
                transparent ? UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha : UnityEngine.Rendering.BlendMode.Zero));
            material.SetInt("_ZWrite", transparent ? 0 : 1);
            material.SetOverrideTag("RenderType", transparent ? "Transparent" : mode == ImageBlendMode.Cutout ? "TransparentCutout" : "Opaque");
            material.renderQueue = transparent ? (int)UnityEngine.Rendering.RenderQueue.Transparent :
                mode == ImageBlendMode.Cutout ? (int)UnityEngine.Rendering.RenderQueue.AlphaTest + 20 : (int)UnityEngine.Rendering.RenderQueue.Geometry;
        }

        protected override void UpdateScale()
        {
            base.UpdateScale();
            if (m_OwnedPlaneMesh == null) { return; }
            float size = m_Size * m_TransitionScale;
            m_ImageQuad.transform.localScale = Vector3.one * size;
            var bounds = m_OwnedPlaneMesh.bounds;
            var extent = bounds.size * Mathf.Abs(size) + Vector3.one * m_ColliderBloat + m_ContainerBloat * 2;
            m_BoxCollider.transform.localScale = Vector3.one;
            m_BoxCollider.transform.localPosition = bounds.center * size;
            m_BoxCollider.size = new Vector3(Mathf.Max(.001f, extent.x), Mathf.Max(.001f, extent.y), Mathf.Max(.001f, extent.z));
            if (m_SnapGhost != null) { m_SnapGhost.localScale = Vector3.one * size; }
        }

        public override void OnPreDestroy()
        {
            if (m_OwnedPlaneMesh != null) { Destroy(m_OwnedPlaneMesh); }
            base.OnPreDestroy();
        }

        public string FileName =>
            m_ReferenceImage?.FileName ?? m_MissingInfo?.fileName ?? Unused("Error");

        public string RelativePath =>
            m_ReferenceImage?.RelativePath ?? m_MissingInfo?.fileName ?? Unused("Error");

        /// width / height
        public override float? AspectRatio =>
            m_ReferenceImage?.ImageAspect ?? m_MissingInfo?.aspectRatio;

        /// Prior to M15, images were incorrectly tinted.
        public bool UseLegacyTint
        {
            get { return m_UseLegacyTint; }
            set
            {
                m_UseLegacyTint = value;
                float tintValue = m_UseLegacyTint ? 1.0f : 0.0f;
                m_ImageQuad.material.SetFloat("_LegacyReferenceImageTint", tintValue);
            }
        }

        /// Alpha multiplier applied to the image. Currently only set by the Quill importer.
        public float Opacity
        {
            get { return m_Opacity; }
            set
            {
                m_Opacity = Mathf.Clamp01(value);
                if (m_ImageQuad != null && m_ImageQuad.material != null)
                {
                    Color color = m_ImageQuad.material.color;
                    color.a = m_Opacity;
                    m_ImageQuad.material.color = color;
                }
            }
        }

        /// Prevents automatic size recalculation when ReferenceImage is set
        public void SetPreserveCustomSize(bool preserve)
        {
            m_PreserveCustomSize = preserve;
        }

        /// Override to check if custom size should be preserved
        protected override bool ShouldPreserveCustomSize()
        {
            return m_PreserveCustomSize;
        }

        /// Public accessor for API to check preserve flag
        public bool ShouldPreserveCustomSizePublic()
        {
            return m_PreserveCustomSize;
        }

        /// Override SetSignedWidgetSize to respect preserve flag
        public new void SetSignedWidgetSize(float fScale)
        {
            if (m_PreserveCustomSize)
            {
                return;
            }
            base.SetSignedWidgetSize(fScale);
        }

        override protected void OnDestroy()
        {
            base.OnDestroy();
            ReleaseTexture();
        }

        public override GrabWidget Clone()
        {
            return Clone(transform.position, transform.rotation, m_Size);
        }

        override public GrabWidget Clone(Vector3 position, Quaternion rotation, float size)
        {
            ImageWidget clone = Instantiate(WidgetManager.m_Instance.ImageWidgetPrefab);
            clone.m_PreviousCanvas = m_PreviousCanvas;
            clone.transform.position = position;
            clone.transform.rotation = rotation;
            // We're obviously not loading from a sketch.  This is to prevent the intro animation.
            // TODO: Change variable name to something more explicit of what this flag does.
            clone.m_LoadingFromSketch = true;
            // We also want to lie about our intro transition amount.
            // TODO: I think this is an old concept and redundant with other intro anim members.
            clone.m_TransitionScale = 1.0f;
            if (m_ReferenceImage != null)
            {
                m_ReferenceImage.SynchronousLoad();
            }
            clone.ReferenceImage = m_ReferenceImage;
            clone.Show(true, false);
            clone.transform.parent = transform.parent;
            clone.SetSignedWidgetSize(size);
            clone.UseLegacyTint = this.m_UseLegacyTint;
            clone.Opacity = this.m_Opacity;
            HierarchyUtils.RecursivelySetLayer(clone.transform, gameObject.layer);
            TiltMeterScript.m_Instance.AdjustMeterWithWidget(clone.GetTiltMeterCost(), up: true);
            clone.CloneInitialMaterials(this);
            clone.TwoSided = TwoSided;
            clone.ApplyLayout(CaptureLayout());
            clone.TrySetCanvasKeywordsFromObject(transform);
            return clone;
        }

        override public void RestoreFromToss()
        {
            base.RestoreFromToss();
            AcquireTexture();
        }

        override protected void OnHide()
        {
            base.OnHide();
            ReleaseTexture();
        }

        public override string GetExportName()
        {
            if (m_ReferenceImage != null)
            {
                return m_ReferenceImage.GetExportName();
            }
            else
            {
                return Path.GetFileNameWithoutExtension(RelativePath);
            }
        }

        private void AcquireTexture()
        {
            if (m_ReferenceImage != null)
            {
                if (!m_TextureAcquired)
                {
                    m_ReferenceImage.AcquireImageFullsize(m_LoadingFromSketch);
                    ImageTexture = m_ReferenceImage.FullSize;
                    m_TextureAcquired = true;
                }
            }
            else
            {
                ImageTexture = m_NoImageTexture;
                m_TextureAcquired = false;
            }
        }

        private void ReleaseTexture()
        {
            if (m_ReferenceImage != null)
            {
                m_ReferenceImage.ReleaseImageFullsize();
                m_TextureAcquired = false;
            }
            ImageTexture = m_NoImageTexture;
        }

        public ReferenceImage ReferenceImage
        {
            set
            {
                //dump old texture
                ReleaseTexture();

                m_ReferenceImage = value;

                AcquireTexture();

                // Remove previous image vertex recording.
                WidgetManager.m_Instance.AdjustImageVertCount(-m_NumVertsTrackedByWidgetManager);
                m_NumVertsTrackedByWidgetManager = 0;

                if (m_ReferenceImage != null)
                {
                    //update the aspect ratio of our mesh to match the image
                    m_Mesh.transform.localScale = Vector3.one * 0.5f;

                    // Only recalculate size if we're not preserving a custom size
                    if (!m_PreserveCustomSize)
                    {
                        var sizeRange = GetWidgetSizeRange();
                        float newSize;
                        if (m_ReferenceImage.ImageAspect > 1)
                        {
                            newSize = Mathf.Clamp(2 / m_ReferenceImage.ImageAspect / Coords.CanvasPose.scale,
                                sizeRange.x, sizeRange.y);
                        }
                        else
                        {
                            newSize = Mathf.Clamp(2 * m_ReferenceImage.ImageAspect / Coords.CanvasPose.scale,
                                sizeRange.x, sizeRange.y);
                        }
                        m_Size = newSize;
                    }
                    UpdateScale();

                    m_NumVertsTrackedByWidgetManager = (int)((m_ReferenceImage.FullSize.width *
                        m_ReferenceImage.FullSize.height) * m_VertCountScalar);
                    WidgetManager.m_Instance.AdjustImageVertCount(m_NumVertsTrackedByWidgetManager);
                }

                // Images are created in the main canvas.
                HierarchyUtils.RecursivelySetLayer(transform, App.Scene.MainCanvas.gameObject.layer);
                HierarchyUtils.RecursivelySetMaterialBatchID(transform, m_BatchId);

                InitSnapGhost(m_ImageQuad.transform, transform);
            }
            get { return m_ReferenceImage; }
        }

        public void SetExtrusion(float depth, Color color)
        {
            var extruder = gameObject.GetComponentInChildren<SpriteExtruder>();
            var importer = new RuntimeSVGImporter();
            var imageMeshRenderer = m_Mesh.GetComponent<MeshRenderer>();
            if (m_ReferenceImage.FilePath.EndsWith(".svg"))
            {
                var extruderMeshFilter = extruder.GetComponent<MeshFilter>();
                if (depth > 0)
                {
                    imageMeshRenderer.enabled = false;
                    var scaleFix = new Vector3(0.002f, -0.002f, 0.5f);
                    var positionFix = new Vector3(-0.5f, 0.5f, 0);
                    var tr = Matrix4x4.TRS(positionFix, Quaternion.identity, scaleFix);
                    var sceneInfo = importer.ImportAsSceneInfo(m_ReferenceImage.FilePath);
                    extruderMeshFilter.mesh = importer.SceneInfoToMesh(sceneInfo, tr, depth);
                }
                else
                {
                    imageMeshRenderer.enabled = false;
                    extruderMeshFilter.mesh = null;
                }
            }
            else
            {
                SpriteRenderer spriteRenderer = gameObject.GetComponentInChildren<SpriteRenderer>();
                spriteRenderer.enabled = true;
                if (depth > 0)
                {
                    Sprite sprite = importer.ImportAsVectorSprite(m_ReferenceImage.FilePath);
                    spriteRenderer.sprite = sprite;
                    extruder.AssignSprite(sprite);
                    extruder.extrudeColor = color;
                    extruder.frontDistance = 0;
                    extruder.backDistance = depth;
                    extruder.Generate();
                }
                else
                {
                    spriteRenderer.enabled = false;
                }
            }
        }

        public bool IsImageValid()
        {
            return m_ReferenceImage != null && m_ReferenceImage.Valid;
        }

        // Get the color of the active texture at the specified (u, v) coordinate.
        public bool GetPixel(float u, float v, out Color pixelColor)
        {
            if (!IsImageValid())
            {
                pixelColor = Color.magenta;
                return false;
            }

            pixelColor = m_ReferenceImage.FullSize.GetPixelBilinear(u, v);
            return true;
        }

        public static void FromTiltImage(TiltImages75 tiltImage)
        {

            var refImage = string.IsNullOrEmpty(tiltImage.FilePath) ?
                ReferenceImageCatalog.m_Instance.FileNameToImage(tiltImage.FileName) :
                ReferenceImageCatalog.m_Instance.RelativePathToImage(tiltImage.FilePath);
            var groupIds = tiltImage.GroupIds;
            var layerIds = tiltImage.LayerIds;
            var twoSidedFlags = tiltImage.TwoSidedFlags;
            var extrusionDepths = tiltImage.ExtrusionDepths;
            var extrusionColors = tiltImage.ExtrusionColors;
            var opacities = tiltImage.Opacities;
            var layouts = tiltImage.Layouts;
            for (int i = 0; i < tiltImage.Transforms.Length; ++i)
            {
                ImageWidget image = Instantiate(WidgetManager.m_Instance.ImageWidgetPrefab);
                image.m_LoadingFromSketch = true;
                image.transform.parent = App.Instance.m_CanvasTransform;
                image.transform.localScale = Vector3.one;
                int layerId = (layerIds == null || i >= layerIds.Length) ? 0 : layerIds[i];
                image.SetCanvas(App.Scene.GetOrCreateLayer(layerId));
                if (refImage != null)
                {
                    refImage.SynchronousLoad();
                    image.ReferenceImage = refImage;
                }
                else
                {
                    image.SetMissing(tiltImage.AspectRatio, tiltImage.FileName);
                }
                image.SetSignedWidgetSize(tiltImage.Transforms[i].scale);
                if (extrusionDepths != null &&
                    extrusionColors != null &&
                    i < extrusionDepths.Length &&
                    i < extrusionColors.Length)
                {
                    image.SetExtrusion(extrusionDepths[i], extrusionColors[i]);
                }
                image.Show(bShow: true, bPlayAudio: false);
                if (opacities != null && i < opacities.Length)
                {
                    image.Opacity = opacities[i];
                }
                image.transform.localPosition = tiltImage.Transforms[i].translation;
                image.transform.localRotation = tiltImage.Transforms[i].rotation;
                if (tiltImage.PinStates[i])
                {
                    image.PinFromSave();
                }
                if (tiltImage.TintStates[i])
                {
                    image.UseLegacyTint = true;
                }
                uint groupId = (groupIds != null && i < groupIds.Length) ? groupIds[i] : 0;
                image.Group = App.GroupManager.GetGroupFromId(groupId);
                image.TwoSided = twoSidedFlags != null && i < twoSidedFlags.Length && twoSidedFlags[i];
                if (layouts != null && i < layouts.Length) { image.ApplyLayout(layouts[i]); }
                TiltMeterScript.m_Instance.AdjustMeterWithWidget(image.GetTiltMeterCost(), up: true);
            }
        }

        public bool HasSubShapes()
        {
            if (m_ReferenceImage == null) return false;

            // SVG image break-apart is not yet implemented, so return false
            // TODO: When SVG image break-apart is implemented, check SvgSceneInfo for sub-shapes
            // var sceneInfo = m_ReferenceImage.SvgSceneInfo;
            // return sceneInfo.Scene?.Root != null && sceneInfo.HasSubShapes();
            return false;
        }
    }
} // namespace TiltBrush
