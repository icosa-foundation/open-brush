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
    public enum ImageBlendMode { Cutout = 0, Opaque = 1, AlphaBlend = 2, Additive = 3 }

    public class ImageWidget : Media2dWidget
    {
        private static T Unused<T>(T value)
        {
            Debug.LogError("Supposedly-unused value is being used");
            return value;
        }

        [SerializeField] private float m_VertCountScalar = 1;

        private bool m_UseLegacyTint;
        private ReferenceImage m_ReferenceImage;
        private bool m_TextureAcquired;
        private bool m_PreserveCustomSize;
        private Color m_ImageColor = Color.white;
        private float? m_AspectRatioOverride;
        private ImageBlendMode m_BlendMode;
        public ImageBlendMode BlendMode
        {
            get => m_BlendMode;
            set => SetBlendMode(value);
        }

        /// Texture multiplier, including opacity. Legacy darkening remains independent.
        public Color ImageColor
        {
            get => m_ImageColor;
            set
            {
                foreach (float component in new[] { value.r, value.g, value.b, value.a })
                {
                    if (float.IsNaN(component) || float.IsInfinity(component))
                    { throw new System.ArgumentException("Image color must be finite"); }
                }
                m_ImageColor = new Color(value.r, value.g, value.b, Mathf.Clamp01(value.a));
                if (m_ImageQuad != null) { m_ImageQuad.material.color = m_ImageColor; }
            }
        }

        public void SetAspectRatioOverride(float? aspect)
        {
            if (aspect.HasValue && (!(aspect.Value > 0) || float.IsInfinity(aspect.Value)))
            { throw new System.ArgumentException("Image aspect ratio must be finite and positive"); }
            m_AspectRatioOverride = aspect;
            UpdateScale();
        }

        private void SetBlendMode(ImageBlendMode mode)
        {
            if (!System.Enum.IsDefined(typeof(ImageBlendMode), mode))
            { throw new System.ArgumentException("Unsupported image blend mode"); }
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

        public string FileName =>
            m_ReferenceImage?.FileName ?? m_MissingInfo?.fileName ?? Unused("Error");

        public string RelativePath =>
            m_ReferenceImage?.RelativePath ?? m_MissingInfo?.fileName ?? Unused("Error");

        /// width / height
        public override float? AspectRatio =>
            m_AspectRatioOverride ?? m_ReferenceImage?.ImageAspect ?? m_MissingInfo?.aspectRatio;

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

        /// Alpha component of the image's RGBA multiplier.
        public float Opacity
        {
            get { return m_ImageColor.a; }
            set
            {
                ImageColor = new Color(m_ImageColor.r, m_ImageColor.g, m_ImageColor.b, value);
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
            clone.ImageColor = m_ImageColor;
            HierarchyUtils.RecursivelySetLayer(clone.transform, gameObject.layer);
            TiltMeterScript.m_Instance.AdjustMeterWithWidget(clone.GetTiltMeterCost(), up: true);
            clone.CloneInitialMaterials(this);
            clone.TwoSided = TwoSided;
            clone.BlendMode = m_BlendMode;
            clone.SetAspectRatioOverride(m_AspectRatioOverride);
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
                m_AspectRatioOverride = null;
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
            var colors = tiltImage.Colors;
            var blendModes = tiltImage.BlendModes;
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
                image.SetAspectRatioOverride(tiltImage.AspectRatio);
                image.SetSignedWidgetSize(tiltImage.Transforms[i].scale);
                if (extrusionDepths != null &&
                    extrusionColors != null &&
                    i < extrusionDepths.Length &&
                    i < extrusionColors.Length)
                {
                    image.SetExtrusion(extrusionDepths[i], extrusionColors[i]);
                }
                image.Show(bShow: true, bPlayAudio: false);
                image.ImageColor = colors != null && i < colors.Length ? colors[i] : Color.white;
                image.BlendMode = blendModes != null && i < blendModes.Length ? blendModes[i] : ImageBlendMode.Cutout;
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
