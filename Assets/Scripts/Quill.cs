// Copyright 2026 The Open Brush Authors
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
using UnityEngine;
using SQ = SharpQuill;

namespace TiltBrush
{
    public static class Quill
    {
        /// <summary>
        /// Options passed when the LoadQuillFile global command fires.
        /// Set before issuing LoadQuillConfirmUnsaved or LoadQuillFile.
        /// </summary>
        public class QuillLoadOptions
        {
            public string Path { get; set; }
            /// <summary>Chapter index to load. -1 = default (first/only chapter).</summary>
            public int ChapterIndex { get; set; } = -1;
        }

        public static QuillLoadOptions PendingLoadOptions { get; set; }

        /// <summary>
        /// Backward-compatible shim. Reads/writes PendingLoadOptions.Path with ChapterIndex=-1.
        /// </summary>
        public static string PendingLoadPath
        {
            get => PendingLoadOptions?.Path;
            set
            {
                if (value == null)
                {
                    PendingLoadOptions = null;
                }
                else if (PendingLoadOptions == null)
                {
                    PendingLoadOptions = new QuillLoadOptions { Path = value };
                }
                else
                {
                    PendingLoadOptions.Path = value;
                }
            }
        }

        /// <summary>
        /// The background color from the most recently loaded Quill sequence, in gamma space.
        /// Null if no Quill file has been loaded yet.
        /// </summary>
        public static Color? LastLoadedBackgroundColor { get; private set; }

        /// <summary>
        /// Filename (relative to App.BackgroundImagesLibraryPath) of a 360 equirectangular image
        /// found in the most recently loaded Quill sequence. Null if none was found.
        /// Only set during a full load, not during merge imports.
        /// </summary>
        public static string LastLoaded360SkyboxName { get; private set; }

        // Quill-specific brush GUIDs
        private const string BRUSH_QUILL_CYLINDER = "f1c4e3e7-2a9f-4b5d-8c3e-7d9a1f8e6b4c";
        private const string BRUSH_QUILL_ELLIPSE = "a2d5f6b8-9c1e-4f3a-7b8d-2e6c9f4a1d5b";
        private const string BRUSH_QUILL_CUBE = "b3e7f8c2-4d5a-1e9b-6c8f-3a7d2f1e9c4b";
        private const string BRUSH_QUILL_RIBBON = "c4f8b3e2-9d1a-5e7f-4c3b-8a6d2f9e1c7b";

        public static void Load(
            string path, int maxStrokes = 0,
            bool loadAnimations = false, string layerName = null,
            bool flattenHierarchy = true, bool layersCanTransform = false,
            int chapterIndex = -1)
        {
            string kind;
            SQ.Sequence sequence = null;
            if (Directory.Exists(path))
            {
                kind = "quill";
                sequence = SQ.QuillSequenceReader.Read(path);
                if (chapterIndex >= 0 && sequence != null)
                {
                    ApplyQuillChapterFilter(sequence, chapterIndex, path);
                }
            }
            else if (File.Exists(path))
            {
                kind = "imm";
                sequence = ImmStrokeReader.SharpQuillCompat.ReadImmAsSequence(path, includePictures: true, chapterIndex: chapterIndex);
            }
            else
            {
                Debug.LogError($"Quill load path not found: {path}");
                return;
            }

            var recorder = new QuillStatsRecorder();
            s_QuillStatsRecorder = recorder;
            try
            {
                if (sequence == null)
                {
                    Debug.LogError("Failed to read Quill sequence");
                    return;
                }

                // Store background color (Quill colors are linear; convert to gamma for Unity)
                var sqBg = sequence.BackgroundColor;
                LastLoadedBackgroundColor = new Color(sqBg.R, sqBg.G, sqBg.B).gamma;
                LastLoaded360SkyboxName = null;

                if (!flattenHierarchy)
                {
                    Debug.LogWarning("Quill hierarchy import not implemented yet; using flattening path.");
                }

                // Recurse layers and collect all strokes
                int strokeCount = 0;
                List<Stroke> allCollectedStrokes = new List<Stroke>();
                List<CanvasScript> createdLayers = new List<CanvasScript>();
                List<GrabWidget> createdWidgets = new List<GrabWidget>();
                Dictionary<string, ReferenceImage> imageCache = new Dictionary<string, ReferenceImage>(StringComparer.OrdinalIgnoreCase);
                string quillImageDirectory = GetQuillImageDirectory(path);
                string quillSoundDirectory = GetQuillSoundDirectory(path);

                // The "correct" 10x global scale correction for Open Brush units creates issues
                // where the scene is hard to see even at max zoom.
                // 1x is a bit too small - so I went with an arbitrary 2x scale
                Matrix4x4 globalCorrection = Matrix4x4.Scale(Vector3.one * 2f);

                // The RootLayer itself can have a transform
                Matrix4x4 rootWorldNoFlip = globalCorrection * ConvertSQTransformMatrix(sequence.RootLayer.Transform, includeFlip: false);
                Matrix4x4 rootWorldWithFlip = globalCorrection * ConvertSQTransformMatrix(sequence.RootLayer.Transform, includeFlip: true);

                bool rootVisible = sequence.RootLayer == null || sequence.RootLayer.Visible;
                float rootOpacity = sequence.RootLayer == null ? 1f : sequence.RootLayer.Opacity;
                if (!rootVisible || rootOpacity <= 0f)
                {
                    return;
                }

                // Iterate only over top-level layers of the root group
                foreach (var topLevelLayer in sequence.RootLayer.Children)
                {
                    // NOTE: IMM imports flatten hierarchy in the adapter, so name filtering
                    // only matches top-level layer names in that path.
                    if (!string.IsNullOrEmpty(layerName) && !LayerContainsName(topLevelLayer, layerName))
                    {
                        continue;
                    }

                    if (!IsLayerRenderable(topLevelLayer, rootVisible, rootOpacity, out _, out _))
                    {
                        continue;
                    }

                    // Create exactly one OB layer for each top-level Quill layer
                    CanvasScript obLayer = App.Scene.AddLayerNow();
                    App.Scene.RenameLayer(obLayer, topLevelLayer.Name);

                    Matrix4x4 localNoFlip = ConvertSQTransformMatrix(topLevelLayer.Transform, includeFlip: false);
                    Matrix4x4 localWithFlip = ConvertSQTransformMatrix(topLevelLayer.Transform, includeFlip: true);

                    // Calculate world transform for this top-level layer
                    Matrix4x4 topLevelWorldNoFlip = rootWorldNoFlip * localNoFlip;
                    Matrix4x4 topLevelWorldWithFlip = rootWorldWithFlip * localWithFlip;

                    // layersCanTransform is currently always false
                    // because layers with non-uniform transforms are buggy
                    // but will be important for later animation functionality
                    if (layersCanTransform)
                    {
                        obLayer.Pose = TrTransform.FromMatrix4x4(topLevelWorldNoFlip);
                    }
                    createdLayers.Add(obLayer);

                    if (recorder != null)
                    {
                        Matrix4x4 pivotMatrix = ConvertSQTransformMatrix(topLevelLayer.Pivot, includeFlip: true);
                        recorder.RecordLayer(topLevelLayer.Name, topLevelWorldNoFlip, topLevelWorldWithFlip, localNoFlip, localWithFlip, pivotMatrix);
                    }

                    // Recurse into children and flatten ALL descendant strokes into this obLayer
                    TraverseAndFlattenLayers(
                        topLevelLayer,
                        topLevelWorldWithFlip,
                        obLayer,
                        ref strokeCount,
                        maxStrokes,
                        loadAnimations,
                        allCollectedStrokes,
                        createdWidgets,
                        imageCache,
                        path,
                        quillImageDirectory,
                        quillSoundDirectory,
                        layerName,
                        includeAllDescendants: false,
                        parentVisible: rootVisible,
                        parentOpacity: rootOpacity);

                    if (maxStrokes > 0 && strokeCount >= maxStrokes) break;
                }

                if (allCollectedStrokes.Count > 0)
                {
                    // Register strokes in memory list first
                    foreach (var stroke in allCollectedStrokes)
                    {
                        SketchMemoryScript.m_Instance.MemoryListAdd(stroke);
                    }

                    // Optimized batch rendering
                    SketchMemoryScript.m_Instance.RenderStrokesDirectly(allCollectedStrokes);

                    // Finalize batches
                    foreach (var layer in createdLayers)
                    {
                        layer.BatchManager.FlushMeshUpdates();
                    }

                }

                if (allCollectedStrokes.Count > 0 || createdWidgets.Count > 0)
                {
                    // Single undo step for all strokes, layers, and widgets
                    var cmd = new LoadQuillCommand(allCollectedStrokes, createdLayers, createdWidgets);
                    SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);

                    if (maxStrokes > 0 && strokeCount >= maxStrokes)
                    {
                        Debug.LogWarning($"Reached maxStrokes limit ({maxStrokes}). Partial load complete.");
                    }
                }
            }
            finally
            {
                QuillDiagnostics.LastLoad = recorder.ToStats(path, kind);
                s_QuillStatsRecorder = null;
            }

        }

        private static void TraverseAndFlattenLayers(
            SQ.Layer layer,
            Matrix4x4 worldXf,
            CanvasScript targetLayer,
            ref int strokeCount,
            int maxStrokes,
            bool loadAnimations,
            List<Stroke> collectedStrokes,
            List<GrabWidget> createdWidgets,
            Dictionary<string, ReferenceImage> imageCache,
            string quillProjectPath,
            string quillImageDirectory,
            string quillSoundDirectory,
            string layerName,
            bool includeAllDescendants,
            bool parentVisible,
            float parentOpacity)
        {
            if (maxStrokes > 0 && strokeCount >= maxStrokes) return;

            if (!IsLayerRenderable(layer, parentVisible, parentOpacity, out bool layerVisible, out float layerOpacity))
            {
                return;
            }

            bool hasFilter = !string.IsNullOrEmpty(layerName);
            bool isMatch = hasFilter && string.Equals(layer.Name, layerName, StringComparison.OrdinalIgnoreCase);
            bool allowAll = includeAllDescendants || isMatch;

            if (hasFilter && !allowAll && !LayerContainsName(layer, layerName))
            {
                return;
            }

            // 1. Process strokes in this layer if it's a Paint layer
            if ((!hasFilter || allowAll) && layer is SQ.LayerPaint paint)
            {
                // NOTE: For now we always call Quill.Load with loadAnimations=false,
                // so only the first frame (or first drawing) is imported.
                IEnumerable<SQ.Drawing> drawingsToLoad;
                if (loadAnimations)
                {
                    drawingsToLoad = paint.Drawings;
                }
                else
                {
                    if (paint.Frames.Count > 0)
                    {
                        int drawingIndex = paint.Frames[0];
                        if (drawingIndex >= 0 && drawingIndex < paint.Drawings.Count)
                        {
                            drawingsToLoad = new[] { paint.Drawings[drawingIndex] };
                        }
                        else
                        {
                            drawingsToLoad = paint.Drawings.Take(1);
                        }
                    }
                    else
                    {
                        drawingsToLoad = paint.Drawings.Take(1);
                    }
                }

                foreach (var drawing in drawingsToLoad)
                {
                    foreach (var sqStroke in drawing.Data.Strokes)
                    {
                        // Calculate transform from Quill world space to the OB targetLayer's local space
                        Matrix4x4 toLayerSpace = targetLayer.Pose.ToMatrix4x4().inverse * worldXf;

                        var tbStroke = ConvertStroke(sqStroke, targetLayer, toLayerSpace, layerOpacity);
                        if (tbStroke != null)
                        {
                            collectedStrokes.Add(tbStroke);
                            strokeCount++;
                        }
                        if (maxStrokes > 0 && strokeCount >= maxStrokes) return;
                    }
                }
            }

            // 2. Process image layers
            if ((!hasFilter || allowAll) && layer is SQ.LayerPicture picture)
            {
                var widget = CreateImageWidgetFromQuillLayer(
                    picture,
                    worldXf,
                    targetLayer,
                    imageCache,
                    quillProjectPath,
                    quillImageDirectory,
                    layerOpacity);
                if (widget != null)
                {
                    createdWidgets.Add(widget);
                }
            }

            // 2b. Process sound layers
            if ((!hasFilter || allowAll) && layer is SQ.LayerSound sound)
            {
                var widget = CreateSoundWidgetFromQuillLayer(
                    sound,
                    worldXf,
                    targetLayer,
                    quillProjectPath,
                    quillSoundDirectory);
                if (widget != null)
                {
                    createdWidgets.Add(widget);
                }
            }

            // 3. Recurse into children if it's a Group layer
            if (layer is SQ.LayerGroup group)
            {
                foreach (var child in group.Children)
                {
                    Matrix4x4 childLocalXf = ConvertSQTransformMatrix(child.Transform, includeFlip: true);
                    Matrix4x4 childWorldXf = worldXf * childLocalXf;
                    TraverseAndFlattenLayers(
                        child,
                        childWorldXf,
                        targetLayer,
                        ref strokeCount,
                        maxStrokes,
                        loadAnimations,
                        collectedStrokes,
                        createdWidgets,
                        imageCache,
                        quillProjectPath,
                        quillImageDirectory,
                        quillSoundDirectory,
                        layerName,
                        includeAllDescendants: allowAll,
                        parentVisible: layerVisible,
                        parentOpacity: layerOpacity);
                    if (maxStrokes > 0 && strokeCount >= maxStrokes) return;
                }
            }
        }

        private static bool IsLayerRenderable(SQ.Layer layer, bool parentVisible, float parentOpacity, out bool visible, out float opacity)
        {
            visible = parentVisible && (layer == null || layer.Visible);
            float layerOpacity = layer == null ? 1f : layer.Opacity;
            opacity = parentOpacity * layerOpacity;
            return visible && opacity > 0f;
        }

        private static bool LayerContainsName(SQ.Layer layer, string layerName)
        {
            if (layer == null || string.IsNullOrEmpty(layerName))
            {
                return false;
            }

            if (string.Equals(layer.Name, layerName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (layer is SQ.LayerGroup group)
            {
                foreach (var child in group.Children)
                {
                    if (LayerContainsName(child, layerName))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static ImageWidget CreateImageWidgetFromQuillLayer(
            SQ.LayerPicture picture,
            Matrix4x4 worldXf,
            CanvasScript targetLayer,
            Dictionary<string, ReferenceImage> imageCache,
            string quillProjectPath,
            string quillImageDirectory,
            float opacity)
        {
            if (picture == null)
            {
                return null;
            }

            int picW = picture.Data?.Width ?? 0;
            int picH = picture.Data?.Height ?? 0;
            int picChannels = picture.Data?.HasAlpha == true ? 4 : 3;
            int actualBytes = picture.Data?.Pixels?.Length ?? 0;
            int expectedRawBytes = picW * picH * picChannels;

            if (picture.PictureType == SQ.PictureType.ThreeSixty_Equirect_Mono ||
                picture.PictureType == SQ.PictureType.ThreeSixty_Equirect_Stereo)
            {
                if (LastLoaded360SkyboxName == null)
                {
                    string skyboxFile = ExtractSkybox360Image(picture, quillProjectPath);
                    if (skyboxFile != null)
                    {
                        LastLoaded360SkyboxName = skyboxFile;
                    }
                    else
                    {
                        Debug.LogWarning($"Quill 360 layer could not be extracted: {picture.Name}");
                    }
                }
                else
                {
                    Debug.LogWarning($"Multiple Quill 360 layers found; only the first will be used as background. Skipping: {picture.Name}");
                }
                return null;
            }

            string sourcePath = ResolveQuillImagePath(picture.ImportFilePath, quillProjectPath);
            ReferenceImage refImage = null;
            if (!string.IsNullOrEmpty(sourcePath))
            {
                refImage = GetOrCreateReferenceImage(sourcePath, imageCache, quillImageDirectory);
            }

            if (refImage == null && picture.Data != null)
            {
                // Quill stores per-layer qbin offsets even when multiple layers share the same ImportFilePath.
                // We treat that as a Quill bug and dedupe by source path when available.
                string importKey = string.IsNullOrEmpty(picture.ImportFilePath)
                    ? null
                    : picture.ImportFilePath.Replace('\\', '/').ToLowerInvariant();
                refImage = GetOrCreateReferenceImageFromPictureData(picture, imageCache, quillImageDirectory, importKey);
            }
            else if (refImage == null && string.IsNullOrEmpty(sourcePath))
            {
                Debug.LogWarning($"Quill picture file not found and no qbin data: {picture.Name}");
            }

            if (refImage == null)
            {
                return null;
            }

            string importLocation = GetImportLocation(refImage);
            if (string.IsNullOrEmpty(importLocation))
            {
                return null;
            }

            TrTransform worldXfTr = TrTransform.FromMatrix4x4(worldXf);
            // Quill maps pictures to a 2x2 quad, with height driving aspect.
            worldXfTr.scale = Mathf.Abs(worldXfTr.scale) * 2.0f;

            ImageWidget image = ApiMethods._ImportImage(importLocation, worldXfTr, targetLayer);
            if (image == null)
            {
                return null;
            }

            image.TwoSided = true;

            ApplyImageOpacity(image, opacity);

            return image;
        }

        private static void ApplyImageOpacity(ImageWidget image, float opacity)
        {
            if (image == null || image.m_ImageQuad == null)
            {
                return;
            }

            float alpha = Mathf.Clamp01(opacity);
            var mat = image.m_ImageQuad.material;
            if (mat == null)
            {
                return;
            }

            Color color = mat.color;
            color.a *= alpha;
            mat.color = color;
        }

        /// <summary>
        /// Extracts a 360-degree picture layer's image into App.BackgroundImagesLibraryPath and
        /// returns the filename (not full path). Returns null on failure.
        /// Handles both file-referenced and embedded (qbin) picture data.
        /// </summary>
        private static string GetQuillBackgroundImageDirectory(string quillProjectPath)
        {
            string projectName = string.IsNullOrEmpty(quillProjectPath)
                ? null
                : SanitizeFileName(Path.GetFileName(quillProjectPath.TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));

            return string.IsNullOrEmpty(projectName)
                ? App.BackgroundImagesLibraryPath()
                : Path.Combine(App.BackgroundImagesLibraryPath(), "Quill", projectName);
        }

        private static string ExtractSkybox360Image(SQ.LayerPicture picture, string quillProjectPath)
        {
            string bgRoot = App.BackgroundImagesLibraryPath();
            string bgDir = GetQuillBackgroundImageDirectory(quillProjectPath);
            Directory.CreateDirectory(bgDir);

            // Try file path first (same approach as 2D image handling)
            string sourcePath = ResolveQuillImagePath(picture.ImportFilePath, quillProjectPath);
            if (!string.IsNullOrEmpty(sourcePath))
            {
                string fileName = Path.GetFileName(sourcePath);
                string destPath = EnsureUniqueImagePath(Path.Combine(bgDir, fileName));
                try
                {
                    File.Copy(sourcePath, destPath, overwrite: false);
                    return Path.GetRelativePath(bgRoot, destPath);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to copy Quill 360 image '{sourcePath}': {e.Message}");
                }
            }

            // Fall back to embedded pixel data (same approach as GetOrCreateReferenceImageFromPictureData)
            if (picture.Data != null)
            {
                var data = picture.Data;
                if (data.Width <= 0 || data.Height <= 0 || data.Pixels == null)
                {
                    Debug.LogWarning($"Quill 360 layer has no usable pixel data: {picture.Name}");
                    return null;
                }

                string rawName = !string.IsNullOrEmpty(picture.ImportFilePath)
                    ? Path.GetFileName(picture.ImportFilePath.Replace('\\', '/'))
                    : (string.IsNullOrEmpty(picture.Name) ? "quill-360" : picture.Name);
                string baseName = SanitizeFileName(Path.GetFileNameWithoutExtension(rawName));
                string destPath = EnsureUniqueImagePath(Path.Combine(bgDir, $"{baseName}.png"));

                try
                {
                    int pixelCount = data.Width * data.Height;
                    int channels = data.Pixels.Length / pixelCount;
                    bool hasAlpha = channels >= 4;
                    var tex = new Texture2D(data.Width, data.Height, TextureFormat.RGBA32, false);
                    var colors = new Color32[pixelCount];
                    int src = 0;
                    for (int i = 0; i < colors.Length; i++)
                    {
                        colors[i] = new Color32(
                            data.Pixels[src],
                            data.Pixels[src + 1],
                            data.Pixels[src + 2],
                            hasAlpha ? data.Pixels[src + 3] : (byte)255);
                        src += channels;
                    }
                    tex.SetPixels32(colors);
                    tex.Apply();
                    File.WriteAllBytes(destPath, tex.EncodeToPNG());
                    UnityEngine.Object.Destroy(tex);
                    return Path.GetRelativePath(bgRoot, destPath);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to write Quill 360 pixel data for '{picture.Name}': {e.Message}");
                }
            }

            return null;
        }

        private static string ResolveQuillImagePath(string importPath, string quillProjectPath)
        {
            if (string.IsNullOrEmpty(importPath))
            {
                return null;
            }

            try
            {
                string resolvedPath = importPath.Replace('/', Path.DirectorySeparatorChar);
                if (!Path.IsPathRooted(resolvedPath) && !string.IsNullOrEmpty(quillProjectPath))
                {
                    resolvedPath = Path.Combine(quillProjectPath, resolvedPath);
                }

                resolvedPath = NormalizePath(resolvedPath);
                if (File.Exists(resolvedPath))
                {
                    return resolvedPath;
                }

                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to resolve Quill picture path '{importPath}': {e.Message}");
                return null;
            }
        }

        private static ReferenceImage GetOrCreateReferenceImage(
            string sourcePath,
            Dictionary<string, ReferenceImage> imageCache,
            string quillImageDirectory)
        {
            string cacheKey = NormalizePath(sourcePath);
            if (imageCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            string homeDir = ReferenceImageCatalog.m_Instance.HomeDirectory;
            string targetDir = string.IsNullOrEmpty(quillImageDirectory) ? homeDir : quillImageDirectory;
            string finalPath = cacheKey;

            if (!sourcePath.StartsWith(targetDir, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string fileName = Path.GetFileName(cacheKey);
                    string destPath = Path.Combine(targetDir, fileName);
                    destPath = EnsureUniqueImagePath(destPath);
                    Directory.CreateDirectory(targetDir);
                    File.Copy(cacheKey, destPath, overwrite: false);
                    finalPath = destPath;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to copy Quill picture into media library: {e.Message}");
                    return null;
                }
            }

            string relativePath = Path.GetRelativePath(homeDir, finalPath);
            ReferenceImage refImage = ReferenceImageCatalog.m_Instance.RelativePathToImage(relativePath);
            imageCache[cacheKey] = refImage;
            return refImage;
        }

        private static ReferenceImage GetOrCreateReferenceImageFromPictureData(
            SQ.LayerPicture picture,
            Dictionary<string, ReferenceImage> imageCache,
            string quillImageDirectory,
            string importKey)
        {
            string cacheKey = !string.IsNullOrEmpty(importKey)
                ? $"path:{importKey}"
                : $"qbin:{picture.DataFileOffset}";
            if (imageCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var data = picture.Data;
            if (data.Width <= 0 || data.Height <= 0 || data.Pixels == null)
            {
                Debug.LogWarning($"Quill picture layer has no pixel data: {picture.Name}");
                return null;
            }

            long pixelCount = (long)data.Width * data.Height;
            if (pixelCount > int.MaxValue)
            {
                Debug.LogWarning($"Quill picture layer too large: {picture.Name}");
                return null;
            }
            int channels = (int)(data.Pixels.Length / pixelCount);
            bool hasAlpha = channels >= 4;
            byte[] pixelBytes = data.Pixels;

            string homeDir = ReferenceImageCatalog.m_Instance.HomeDirectory;
            string targetDir = string.IsNullOrEmpty(quillImageDirectory) ? homeDir : quillImageDirectory;
            string rawFileName = !string.IsNullOrEmpty(picture.ImportFilePath)
                ? Path.GetFileName(picture.ImportFilePath.Replace('\\', '/'))
                : (string.IsNullOrEmpty(picture.Name) ? "quill-image" : picture.Name);
            string fileName = SanitizeFileName(Path.GetFileNameWithoutExtension(rawFileName));
            string destPath = Path.Combine(targetDir, $"{fileName}.png");
            destPath = EnsureUniqueImagePath(destPath);

            try
            {
                var tex = new Texture2D(data.Width, data.Height, TextureFormat.RGBA32, false);
                var colors = new Color32[data.Width * data.Height];
                int srcIndex = 0;
                for (int i = 0; i < colors.Length; i++)
                {
                    colors[i] = new Color32(
                        pixelBytes[srcIndex],
                        pixelBytes[srcIndex + 1],
                        pixelBytes[srcIndex + 2],
                        hasAlpha ? pixelBytes[srcIndex + 3] : (byte)255);
                    srcIndex += channels;
                }
                tex.SetPixels32(colors);
                tex.Apply();

                byte[] png = tex.EncodeToPNG();
                UnityEngine.Object.Destroy(tex);

                Directory.CreateDirectory(targetDir);
                File.WriteAllBytes(destPath, png);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to write Quill picture data: {e.Message}");
                return null;
            }

            string relativePath = Path.GetRelativePath(homeDir, destPath);
            ReferenceImage refImage = ReferenceImageCatalog.m_Instance.RelativePathToImage(relativePath);
            imageCache[cacheKey] = refImage;
            return refImage;
        }

        private static string GetQuillImageDirectory(string quillProjectPath)
        {
            if (string.IsNullOrEmpty(quillProjectPath))
            {
                return null;
            }

            string projectName = Path.GetFileName(quillProjectPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            projectName = SanitizeFileName(projectName);
            if (string.IsNullOrEmpty(projectName))
            {
                return null;
            }

            string homeDir = ReferenceImageCatalog.m_Instance.HomeDirectory;
            string baseDir = Path.Combine(homeDir, "Quill", projectName);
            if (!Directory.Exists(baseDir))
            {
                return baseDir;
            }

            for (int i = 1; i < 1000; i++)
            {
                string candidate = $"{baseDir}-{i}";
                if (!Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            return $"{baseDir}-{Guid.NewGuid()}";
        }
        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return string.IsNullOrEmpty(name) ? "quill-image" : name;
        }

        private static string NormalizePath(string path)
        {
            return Path.GetFullPath(path.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string GetImportLocation(ReferenceImage referenceImage)
        {
            if (referenceImage == null)
            {
                return null;
            }

            string homeDir = App.ReferenceImagePath();
            string fullPath = referenceImage.FileFullPath;
            if (!fullPath.StartsWith(homeDir, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"Quill image not under reference image path: {fullPath}");
                return null;
            }

            string relativePath = Path.GetRelativePath(homeDir, fullPath).Replace('\\', '/');
            if (relativePath.StartsWith("./", StringComparison.Ordinal) || relativePath.StartsWith(".\\", StringComparison.Ordinal))
            {
                relativePath = relativePath.Substring(2);
            }

            return relativePath;
        }

        private static string EnsureUniqueImagePath(string destPath)
        {
            if (!File.Exists(destPath))
            {
                return destPath;
            }

            string directory = Path.GetDirectoryName(destPath);
            string fileName = Path.GetFileNameWithoutExtension(destPath);
            string extension = Path.GetExtension(destPath);

            for (int i = 1; i < 1000; i++)
            {
                string candidate = Path.Combine(directory, $"{fileName}-{i}{extension}");
                if (!File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return Path.Combine(directory, $"{fileName}-{Guid.NewGuid()}{extension}");
        }

        private static SoundClipWidget CreateSoundWidgetFromQuillLayer(
            SQ.LayerSound sound,
            Matrix4x4 worldXf,
            CanvasScript targetLayer,
            string quillProjectPath,
            string quillSoundDirectory)
        {
            if (sound == null)
            {
                return null;
            }

            string sourcePath = null;
            SoundClip soundClip = null;

            // First try external file
            if (!string.IsNullOrEmpty(sound.ImportFilePath))
            {
                sourcePath = ResolveQuillSoundPath(sound.ImportFilePath, quillProjectPath);
                if (!string.IsNullOrEmpty(sourcePath))
                {
                    soundClip = GetOrCreateSoundClip(sourcePath, quillSoundDirectory);
                }
            }

            // If no external file, try embedded data
            if (soundClip == null && sound.Data != null && sound.Data.AudioBytes != null && sound.Data.AudioBytes.Length > 0)
            {
                sourcePath = ExtractEmbeddedAudio(sound, quillSoundDirectory);
                if (!string.IsNullOrEmpty(sourcePath))
                {
                    soundClip = GetOrCreateSoundClip(sourcePath, quillSoundDirectory);
                }
            }

            if (soundClip == null)
            {
                Debug.LogWarning($"Quill sound file not found: {sound.Name}");
                return null;
            }

            TrTransform worldXfTr = TrTransform.FromMatrix4x4(worldXf);
            // Use a default size of 2 for sound widgets (similar to images)
            worldXfTr.scale = Mathf.Abs(worldXfTr.scale) * 2.0f;

            // Create the widget using the same pattern as image widgets
            var previousCanvas = App.Scene.ActiveCanvas;
            try
            {
                App.Scene.ActiveCanvas = targetLayer;
                SoundClipWidget soundWidget = UnityEngine.Object.Instantiate(WidgetManager.m_Instance.SoundClipWidgetPrefab);
                soundWidget.transform.parent = targetLayer.transform;
                soundWidget.transform.localScale = Vector3.one;
                soundWidget.SetSoundClip(soundClip);
                soundWidget.SetSignedWidgetSize(worldXfTr.scale);
                soundWidget.Show(bShow: true, bPlayAudio: false);
                soundWidget.transform.position = worldXfTr.translation;
                soundWidget.transform.rotation = worldXfTr.rotation;

                // Apply Quill sound properties (queued until controller initializes)
                float spatialBlend = sound.SoundType == SharpQuill.SoundType.Flat ? 0f : 1f;
                float minDist = sound.Attenuation?.Minimum ?? 1f;
                float maxDist = sound.Attenuation?.Maximum ?? 500f;
                soundWidget.SetAudioProperties(
                    sound.Gain, sound.Loop, spatialBlend, minDist, maxDist);

                return soundWidget;
            }
            finally
            {
                App.Scene.ActiveCanvas = previousCanvas;
            }
        }

        private static string ResolveQuillSoundPath(string importPath, string quillProjectPath)
        {
            if (string.IsNullOrEmpty(importPath))
            {
                return null;
            }

            try
            {
                string resolvedPath = importPath.Replace('/', Path.DirectorySeparatorChar);
                if (!Path.IsPathRooted(resolvedPath) && !string.IsNullOrEmpty(quillProjectPath))
                {
                    resolvedPath = Path.Combine(quillProjectPath, resolvedPath);
                }

                resolvedPath = NormalizePath(resolvedPath);
                if (File.Exists(resolvedPath))
                {
                    return resolvedPath;
                }

                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to resolve Quill sound path '{importPath}': {e.Message}");
                return null;
            }
        }

        private static SoundClip GetOrCreateSoundClip(string sourcePath, string quillSoundDirectory)
        {
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            {
                return null;
            }

            string homeDir = App.SoundClipLibraryPath();
            string targetDir = string.IsNullOrEmpty(quillSoundDirectory) ? homeDir : quillSoundDirectory;
            string finalPath = sourcePath;

            // If the source file is not already in the sound library, copy it to the Quill subfolder
            if (!sourcePath.StartsWith(homeDir, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string fileName = Path.GetFileName(sourcePath);
                    string destPath = Path.Combine(targetDir, fileName);
                    destPath = EnsureUniqueImagePath(destPath); // Reuse the unique path logic
                    Directory.CreateDirectory(targetDir);
                    File.Copy(sourcePath, destPath, overwrite: false);
                    finalPath = destPath;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to copy Quill sound into library: {e.Message}");
                    return null;
                }
            }

            // Check if it's already in the catalog
            string relativePath = Path.GetRelativePath(homeDir, finalPath);
            SoundClip soundClip = SoundClipCatalog.Instance.GetSoundClipByPersistentPath(relativePath);

            // If not in catalog yet, create a new SoundClip directly
            if (soundClip == null)
            {
                try
                {
                    soundClip = new SoundClip(finalPath);
                    // Trigger catalog rescan in background so it shows up in UI later
                    SoundClipCatalog.Instance.ForceCatalogScan();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to create SoundClip: {e.Message}");
                    return null;
                }
            }

            return soundClip;
        }

        private static string GetQuillSoundDirectory(string quillProjectPath)
        {
            if (string.IsNullOrEmpty(quillProjectPath))
            {
                return null;
            }

            string projectName = Path.GetFileName(quillProjectPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            projectName = SanitizeFileName(projectName);
            if (string.IsNullOrEmpty(projectName))
            {
                return null;
            }

            string homeDir = App.SoundClipLibraryPath();
            string baseDir = Path.Combine(homeDir, "Quill", projectName);
            if (!Directory.Exists(baseDir))
            {
                return baseDir;
            }

            for (int i = 1; i < 1000; i++)
            {
                string candidate = $"{baseDir}-{i}";
                if (!Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            return $"{baseDir}-{Guid.NewGuid()}";
        }

        private static string ExtractEmbeddedAudio(SQ.LayerSound sound, string quillSoundDirectory)
        {
            if (sound.Data == null || sound.Data.AudioBytes == null || sound.Data.AudioBytes.Length == 0)
            {
                return null;
            }

            try
            {
                // Detect audio format from magic bytes
                string extension = DetectAudioFormat(sound.Data.AudioBytes);

                // Create filename
                string baseName = !string.IsNullOrEmpty(sound.Name) ? SanitizeFileName(sound.Name) : "embedded_audio";
                string fileName = $"{baseName}{extension}";

                // Ensure directory exists
                Directory.CreateDirectory(quillSoundDirectory);

                // Write audio file
                string destPath = Path.Combine(quillSoundDirectory, fileName);
                destPath = EnsureUniqueImagePath(destPath); // Reuse unique path logic
                File.WriteAllBytes(destPath, sound.Data.AudioBytes);

                return destPath;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to extract embedded audio for '{sound.Name}': {e.Message}");
                return null;
            }
        }

        private static string DetectAudioFormat(byte[] audioBytes)
        {
            if (audioBytes == null || audioBytes.Length < 12)
            {
                return ".bin"; // Unknown format
            }

            // Check for common audio format magic bytes
            // Quill stores audio as WAV in qbin, so check that first

            // WAV/RIFF: "RIFF" ... "WAVE"
            if (audioBytes.Length >= 12 &&
                audioBytes[0] == 'R' && audioBytes[1] == 'I' &&
                audioBytes[2] == 'F' && audioBytes[3] == 'F' &&
                audioBytes[8] == 'W' && audioBytes[9] == 'A' &&
                audioBytes[10] == 'V' && audioBytes[11] == 'E')
            {
                return ".wav";
            }

            // OGG: "OggS"
            if (audioBytes.Length >= 4 &&
                audioBytes[0] == 'O' && audioBytes[1] == 'g' &&
                audioBytes[2] == 'g' && audioBytes[3] == 'S')
            {
                return ".ogg";
            }

            // MP3: FF FB or FF F3 or FF F2 or ID3
            if (audioBytes[0] == 0xFF && (audioBytes[1] & 0xE0) == 0xE0)
            {
                return ".mp3";
            }
            if (audioBytes.Length >= 3 && audioBytes[0] == 'I' && audioBytes[1] == 'D' && audioBytes[2] == '3')
            {
                return ".mp3";
            }

            // FLAC: "fLaC"
            if (audioBytes.Length >= 4 &&
                audioBytes[0] == 'f' && audioBytes[1] == 'L' &&
                audioBytes[2] == 'a' && audioBytes[3] == 'C')
            {
                return ".flac";
            }

            // Default to WAV since that's what Quill uses in qbin
            Debug.LogWarning("Could not detect audio format, defaulting to .wav");
            return ".wav";
        }

        private static Matrix4x4 ConvertSQTransformMatrix(SQ.Transform sqXf, bool includeFlip)
        {
            // Build the right-handed matrix first.
            Vector3 pos = new Vector3(sqXf.Translation.X, sqXf.Translation.Y, sqXf.Translation.Z);
            Quaternion rot = new Quaternion(sqXf.Rotation.X, sqXf.Rotation.Y, sqXf.Rotation.Z, sqXf.Rotation.W);
            Vector3 scale = Vector3.one * sqXf.Scale;
            Matrix4x4 rh = Matrix4x4.TRS(pos, rot, scale);

            if (includeFlip && !string.IsNullOrEmpty(sqXf.Flip) && sqXf.Flip != "N")
            {
                Vector3 flipAxis = Vector3.one;
                switch (sqXf.Flip)
                {
                    case "X":
                        flipAxis = new Vector3(-1, 1, 1);
                        break;
                    case "Y":
                        flipAxis = new Vector3(1, -1, 1);
                        break;
                    case "Z":
                        flipAxis = new Vector3(1, 1, -1);
                        break;
                }
                rh = rh * Matrix4x4.Scale(flipAxis);
            }

            // Convert to left-handed by flipping Z in both input and output spaces.
            Matrix4x4 mirror = Matrix4x4.Scale(new Vector3(1, 1, -1));
            return mirror * rh * mirror;
        }

        /// <summary>
        /// Computes tangent at a control point by averaging forward and backward differences.
        /// This matches Quill's official importer behavior (Element::ComputeTangent).
        /// Ignores Quill's stored tangent data to achieve smooth interpolation.
        /// </summary>
        private static Vector3 ComputeTangentFromPositions(List<SQ.Vertex> vertices, int index)
        {
            const float kEpsilon = 1e-7f;
            Vector3 currentPos = new Vector3(vertices[index].Position.X, vertices[index].Position.Y, -vertices[index].Position.Z);

            // Find first valid forward difference
            Vector3 forward = Vector3.zero;
            for (int j = index + 1; j < vertices.Count; j++)
            {
                Vector3 nextPos = new Vector3(vertices[j].Position.X, vertices[j].Position.Y, -vertices[j].Position.Z);
                Vector3 delta = nextPos - currentPos;
                if (delta.sqrMagnitude >= kEpsilon * kEpsilon)
                {
                    forward = delta.normalized;
                    break;
                }
            }

            // Find first valid backward difference
            Vector3 backward = Vector3.zero;
            for (int j = index - 1; j >= 0; j--)
            {
                Vector3 prevPos = new Vector3(vertices[j].Position.X, vertices[j].Position.Y, -vertices[j].Position.Z);
                Vector3 delta = currentPos - prevPos;
                if (delta.sqrMagnitude >= kEpsilon * kEpsilon)
                {
                    backward = delta.normalized;
                    break;
                }
            }

            // Average the two directions
            Vector3 tangent = forward + backward;
            if (tangent.sqrMagnitude >= kEpsilon * kEpsilon)
            {
                return tangent.normalized;
            }

            // Fallback: overall stroke direction with tiny offset to avoid zero vector
            Vector3 first = new Vector3(vertices[0].Position.X, vertices[0].Position.Y, -vertices[0].Position.Z);
            Vector3 last = new Vector3(vertices[^1].Position.X, vertices[^1].Position.Y, -vertices[^1].Position.Z);
            tangent = last - first + new Vector3(0.000001f, 0.000002f, 0.000003f);
            return tangent.normalized;
        }

        /// <summary>
        /// Builds a safe orthonormal orientation from forward and up vectors.
        /// This matches Quill's official importer behavior (Element::ComputeBasis).
        /// </summary>
        private static Quaternion BuildSafeOrientation(Vector3 fwd, Vector3 up)
        {
            const float epsilon = 1e-7f;
            Vector3 right = Vector3.Cross(up, fwd);

            if (right.sqrMagnitude >= epsilon * epsilon)
            {
                right.Normalize();
            }
            else if (Mathf.Abs(fwd.x) < 0.9f)
            {
                right = new Vector3(0, fwd.z, fwd.y).normalized;
            }
            else if (Mathf.Abs(fwd.y) < 0.9f)
            {
                right = new Vector3(-fwd.z, 0, fwd.x).normalized;
            }
            else
            {
                right = new Vector3(fwd.y, -fwd.x, 0).normalized;
            }

            up = Vector3.Cross(fwd, right).normalized;
            return Quaternion.LookRotation(fwd, up);
        }


        /// <summary>
        /// Maps Quill width to Open Brush pressure.
        /// Currently uses linear mapping but can be extended for per-brush tuning.
        /// </summary>
        private static float MapPressure(float width, float maxWidth)
        {
            if (maxWidth <= 0f) return 1f;

            // Linear mapping for now
            float pressure = width / maxWidth;

            // Future: Add per-brush pressure curves here if needed
            // switch (brushGuid)
            // {
            //     case BRUSH_QUILL_CYLINDER:
            //         pressure = Mathf.Pow(pressure, 1.2f); // Example adjustment
            //         break;
            // }

            return Mathf.Clamp01(pressure);
        }

        private static Stroke ConvertStroke(SQ.Stroke sqStroke, CanvasScript targetLayer, Matrix4x4 toLayerSpace, float opacityScale)
        {
            if (sqStroke.Vertices.Count < 2) return null;

            // Calculate max width for thresholding
            float maxWidth = 0f;
            foreach (var v in sqStroke.Vertices)
            {
                if (v.Width > maxWidth) maxWidth = v.Width;
            }

            // Determine Brush GUID - map to Quill-specific brushes
            string brushGuid;

            switch (sqStroke.BrushType)
            {
                case SQ.BrushType.Cylinder:
                    brushGuid = BRUSH_QUILL_CYLINDER;
                    break;
                case SQ.BrushType.Ellipse:
                    brushGuid = BRUSH_QUILL_ELLIPSE;
                    break;
                case SQ.BrushType.Cube:
                    brushGuid = BRUSH_QUILL_CUBE;
                    break;
                case SQ.BrushType.Ribbon:
                    brushGuid = BRUSH_QUILL_RIBBON;
                    break;
                default:
                    Debug.LogWarning($"Unknown Quill brush type: {sqStroke.BrushType}, falling back to Ribbon");
                    brushGuid = BRUSH_QUILL_RIBBON;
                    break;
            }

            var brush = BrushCatalog.m_Instance.GetBrush(new Guid(brushGuid));
            if (brush == null)
            {
                brush = BrushCatalog.m_Instance.GetBrush(new Guid(BRUSH_QUILL_RIBBON));
            }
            if (brush == null) return null;

            var color = sqStroke.Vertices[0].Color;
            var unityColor = new Color(color.R, color.G, color.B).gamma;

            var controlPoints = new List<PointerManager.ControlPoint>(sqStroke.Vertices.Count);
            List<Color32?> perPointColors = new List<Color32?>();
            uint time = 0;
            Quaternion prevOrientation = Quaternion.identity;

            for (int i = 0; i < sqStroke.Vertices.Count; i++)
            {
                var v = sqStroke.Vertices[i];

                // Convert Quill vertex position (Right-Handed) to Unity (Left-Handed) by flipping Z
                Vector3 localPos = new Vector3(v.Position.X, v.Position.Y, -v.Position.Z);

                // This matches the working Python importer and creates smooth interpolation
                Vector3 localForward = ComputeTangentFromPositions(sqStroke.Vertices, i);

                // Use Quill's normal for orientation
                Vector3 localUp = new Vector3(v.Normal.X, v.Normal.Y, -v.Normal.Z);

                // Apply relative transform to keep coordinates local to the top-level OB layer
                Vector3 obPos = toLayerSpace.MultiplyPoint(localPos);
                Vector3 obForward = toLayerSpace.MultiplyVector(localForward);
                Vector3 obUp = toLayerSpace.MultiplyVector(localUp);

                // Build safe orthonormal orientation with fallback
                Quaternion orient = BuildSafeOrientation(obForward, obUp);
                prevOrientation = orient;

                // Map width to pressure with potential per-brush tuning
                float pressure = MapPressure(v.Width, maxWidth);

                controlPoints.Add(new PointerManager.ControlPoint
                {
                    m_Pos = obPos,
                    m_Orient = orient,
                    m_Pressure = pressure,
                    m_TimestampMs = time++
                });

                // Capture per-point color from Quill vertex
                float pointAlpha = Mathf.Clamp01(v.Opacity * opacityScale);
                perPointColors.Add(new Color32(
                    (byte)(Mathf.LinearToGammaSpace(v.Color.R) * 255f),
                    (byte)(Mathf.LinearToGammaSpace(v.Color.G) * 255f),
                    (byte)(Mathf.LinearToGammaSpace(v.Color.B) * 255f),
                    (byte)(pointAlpha * 255f)
                ));
            }

            float layerScale = GetUniformScale(toLayerSpace);
            var stroke = new Stroke
            {
                m_Type = Stroke.Type.NotCreated,
                m_IntendedCanvas = targetLayer,
                m_BrushGuid = brush.m_Guid,
                m_BrushScale = 1f,
                // Quill width appears to be radius, but OB expects diameter (or vice versa)
                // Multiply by 2 to match Quill's visual appearance
                m_BrushSize = maxWidth * layerScale * 2f,
                m_Color = unityColor,
                m_Seed = 0,
                m_ControlPoints = controlPoints.ToArray(),
                m_OverrideColors = perPointColors,
                m_ColorOverrideMode = ColorOverrideMode.Replace
            };

            float brushSize = maxWidth * layerScale * 2f;
            if (s_QuillStatsRecorder != null)
            {
                s_QuillStatsRecorder.RecordStroke(maxWidth, brushSize, layerScale, sqStroke, brushGuid);
            }


            stroke.m_ControlPointsToDrop = Enumerable.Repeat(false, stroke.m_ControlPoints.Length).ToArray();
            stroke.Group = SketchGroupTag.None;

            return stroke;
        }

        /// <summary>
        /// Returns the number of chapters in a Quill project directory.
        /// A chapter is defined by Action animation keys on the root layer (Stop/Play markers).
        /// Follows the same logic as IMM: Play markers take priority, then Stop markers.
        /// Returns 1 if no action markers are found (the whole project is a single chapter).
        /// Reads only Quill.json — does NOT load the qbin binary stroke data.
        /// </summary>
        public static int GetQuillChapterCount(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
            {
                return 0;
            }

            string quillJson = Path.Combine(projectPath, "Quill.json");
            if (!File.Exists(quillJson))
            {
                return 0;
            }

            try
            {
                string json = File.ReadAllText(quillJson);
                var doc = Newtonsoft.Json.Linq.JToken.Parse(json);
                var actionKeys = doc["Sequence"]?["RootLayer"]?["Animation"]?["Keys"]?["Action"]
                    as Newtonsoft.Json.Linq.JArray;
                
                if (actionKeys == null || actionKeys.Count == 0)
                {
                    return 1; // No action markers = single chapter
                }

                var playTimes = new List<float>();
                var stopTimes = new List<float>();
                
                foreach (var actionKey in actionKeys)
                {
                    float? markerTime = actionKey["Time"]?.ToObject<float?>();
                    if (!markerTime.HasValue || markerTime.Value < 0f)
                    {
                        continue;
                    }

                    string actionValue = actionKey["Value"]?.ToObject<string>()?.Trim();
                    if (string.Equals(actionValue, "Play", StringComparison.OrdinalIgnoreCase))
                    {
                        playTimes.Add(markerTime.Value);
                    }
                    else if (string.Equals(actionValue, "Stop", StringComparison.OrdinalIgnoreCase))
                    {
                        stopTimes.Add(markerTime.Value);
                    }
                }

                var chapterStarts = new HashSet<float> { 0f };

                if (playTimes.Count > 0)
                {
                    foreach (var playTime in playTimes)
                    {
                        chapterStarts.Add(playTime);
                    }
                }
                else if (stopTimes.Count > 0)
                {
                    foreach (var stopTime in stopTimes)
                    {
                        chapterStarts.Add(stopTime + 1f);
                    }
                }

                return chapterStarts.Count;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[QUILL-CHAPTER] Failed to read chapter count from '{projectPath}': {e.Message}");
                return 1;
            }
        }

        /// <summary>
        /// Applies chapter selection using root Action keys (IMM-compatible logic), then samples the
        /// full sequence to the selected chapter start time for static display.
        /// </summary>
        private static void ApplyQuillChapterFilter(SQ.Sequence sequence, int chapterIndex, string path)
        {
            var actionKeys = sequence?.RootLayer?.Animation?.Keys?.Action;
            int actionKeyCount = actionKeys?.Count ?? 0;
            if (actionKeyCount == 0)
            {
                return;
            }

            bool hasPlayMarkers = false;
            bool hasStopMarkers = false;
            foreach (var actionKey in actionKeys)
            {
                if (actionKey.Time < 0f)
                {
                    continue;
                }

                string action = actionKey.Value?.Trim();
                if (string.Equals(action, "Play", StringComparison.OrdinalIgnoreCase))
                {
                    hasPlayMarkers = true;
                }
                else if (string.Equals(action, "Stop", StringComparison.OrdinalIgnoreCase))
                {
                    hasStopMarkers = true;
                }
            }

            string markerType = hasPlayMarkers ? "Play" : hasStopMarkers ? "Stop" : "none";
            List<float> chapterStarts = CalculateChapterStartTimes(actionKeys);
            int chapterCount = chapterStarts.Count;

            if (chapterIndex < 0 || chapterIndex >= chapterCount)
            {
                Debug.LogWarning($"[QUILL-CHAPTER] Requested chapter {chapterIndex} out of range (count={chapterCount}), loading chapter 0.");
                chapterIndex = 0;
            }

            float chapterStart = chapterStarts[chapterIndex];
            float chapterEnd = chapterIndex + 1 < chapterCount
                ? chapterStarts[chapterIndex + 1]
                : float.PositiveInfinity;

            SetSequenceToChapterTime(sequence, chapterStart);
        }

        private static List<float> CalculateChapterStartTimes(List<SQ.Keyframe<string>> actionKeys)
        {
            var chapterStarts = new List<float> { 0f };
            if (actionKeys == null || actionKeys.Count == 0)
            {
                return chapterStarts;
            }

            var playTimes = new List<float>();
            var stopTimes = new List<float>();

            foreach (var actionKey in actionKeys.OrderBy(k => k.Time))
            {
                float time = actionKey.Time;
                if (time < 0f)
                {
                    continue;
                }

                string action = actionKey.Value?.Trim();
                if (string.Equals(action, "Play", StringComparison.OrdinalIgnoreCase))
                {
                    playTimes.Add(time);
                }
                else if (string.Equals(action, "Stop", StringComparison.OrdinalIgnoreCase))
                {
                    stopTimes.Add(time);
                }
            }

            if (playTimes.Count > 0)
            {
                chapterStarts.AddRange(playTimes);
            }
            else if (stopTimes.Count > 0)
            {
                chapterStarts.AddRange(stopTimes.Select(t => t + 1f));
            }

            return chapterStarts
                .Distinct()
                .OrderBy(t => t)
                .ToList();
        }

        private static void SetSequenceToChapterTime(SQ.Sequence sequence, float chapterTime)
        {
            if (sequence?.RootLayer == null)
            {
                return;
            }

            SetLayerToTime(sequence.RootLayer, chapterTime);
        }

        private static void SetLayerToTime(SQ.Layer layer, float time)
        {
            if (layer == null)
            {
                return;
            }

            var keys = layer.Animation?.Keys;
            if (keys != null)
            {
                layer.Visible = SampleKeyframes(keys.Visibility, time, layer.Visible);
                layer.Opacity = SampleKeyframes(keys.Opacity, time, layer.Opacity);
                layer.Transform = SampleKeyframes(keys.Transform, time, layer.Transform);
            }

            if (layer is SQ.LayerGroup group)
            {
                foreach (var child in group.Children)
                {
                    SetLayerToTime(child, time);
                }
            }
        }

        private static T SampleKeyframes<T>(List<SQ.Keyframe<T>> keyframes, float time, T defaultValue)
        {
            if (keyframes == null || keyframes.Count == 0)
            {
                return defaultValue;
            }

            T sampledValue = defaultValue;
            bool foundAny = false;
            foreach (var keyframe in keyframes.OrderBy(k => k.Time))
            {
                if (keyframe.Time > time)
                {
                    break;
                }

                sampledValue = keyframe.Value;
                foundAny = true;
            }

            return foundAny ? sampledValue : defaultValue;
        }

        private static float GetUniformScale(Matrix4x4 m)
        {
            Vector3 x = new Vector3(m.m00, m.m10, m.m20);
            float scale = x.magnitude;
            if (scale > 0)
            {
                return scale;
            }
            Vector3 y = new Vector3(m.m01, m.m11, m.m21);
            scale = y.magnitude;
            if (scale > 0)
            {
                return scale;
            }
            Vector3 z = new Vector3(m.m02, m.m12, m.m22);
            return z.magnitude;
        }

        private static float[] MatrixToArray(Matrix4x4 m)
        {
            return new float[]
            {
                m.m00, m.m01, m.m02, m.m03,
                m.m10, m.m11, m.m12, m.m13,
                m.m20, m.m21, m.m22, m.m23,
                m.m30, m.m31, m.m32, m.m33
            };
        }

        public class QuillLoadStats
        {
            public string Path;
            public string Kind;
            public string TimestampUtc;
            public int StrokeCount;
            public int VertexCount;
            public FloatStats VertexWidth;
            public FloatStats StrokeMaxWidth;
            public FloatStats StrokeBrushSize;
            public FloatStats StrokeLayerScale;
            public BoundingBoxStats StrokeBoundingBox;
            public ColorChannelStats VertexColor;
            public List<LayerTransformStats> Layers;
            public Dictionary<string, BrushStats> Brushes;
        }

        public class LayerTransformStats
        {
            public string Name;
            public float[] WorldNoFlip;
            public float[] WorldWithFlip;
            public float[] LocalNoFlip;
            public float[] LocalWithFlip;
            public float[] Pivot;
        }

        public class BoundingBoxStats
        {
            public FloatStats SizeX;
            public FloatStats SizeY;
            public FloatStats SizeZ;
            public FloatStats Diagonal;
        }

        public class ColorChannelStats
        {
            public FloatStats R;
            public FloatStats G;
            public FloatStats B;
            public FloatStats Opacity;
        }

        public class BrushStats
        {
            public string BrushGuid;
            public int BrushType;
            public int StrokeCount;
            public int VertexCount;
            public FloatStats VertexWidth;
            public FloatStats StrokeMaxWidth;
            public FloatStats StrokeBrushSize;
        }

        public struct FloatStats
        {
            public int Count;
            public float Min;
            public float P50;
            public float P90;
            public float Max;
            public float Mean;
        }

        public static class QuillDiagnostics
        {
            public static QuillLoadStats LastLoad;
        }

        private sealed class QuillStatsRecorder
        {
            private const int MaxSamples = 200000;

            private readonly List<float> _vertexWidths = new List<float>();
            private readonly List<float> _strokeMaxWidths = new List<float>();
            private readonly List<float> _strokeBrushSizes = new List<float>();
            private readonly List<float> _strokeLayerScales = new List<float>();
            private readonly List<float> _bboxSizeX = new List<float>();
            private readonly List<float> _bboxSizeY = new List<float>();
            private readonly List<float> _bboxSizeZ = new List<float>();
            private readonly List<float> _bboxDiagonal = new List<float>();
            private readonly List<float> _colorR = new List<float>();
            private readonly List<float> _colorG = new List<float>();
            private readonly List<float> _colorB = new List<float>();
            private readonly List<float> _opacity = new List<float>();
            private readonly Dictionary<string, BrushAccumulator> _brushes = new Dictionary<string, BrushAccumulator>(StringComparer.OrdinalIgnoreCase);
            private readonly List<LayerTransformStats> _layers = new List<LayerTransformStats>();
            private int _vertexCount;

            public void RecordLayer(string name, Matrix4x4 worldNoFlip, Matrix4x4 worldWithFlip, Matrix4x4 localNoFlip, Matrix4x4 localWithFlip, Matrix4x4 pivot)
            {
                _layers.Add(new LayerTransformStats
                {
                    Name = name,
                    WorldNoFlip = MatrixToArray(worldNoFlip),
                    WorldWithFlip = MatrixToArray(worldWithFlip),
                    LocalNoFlip = MatrixToArray(localNoFlip),
                    LocalWithFlip = MatrixToArray(localWithFlip),
                    Pivot = MatrixToArray(pivot)
                });
            }

            public void RecordStroke(float strokeMaxWidth, float brushSize, float layerScale, SQ.Stroke stroke, string brushGuid)
            {
                AddSample(_strokeMaxWidths, strokeMaxWidth);
                AddSample(_strokeBrushSizes, brushSize);
                AddSample(_strokeLayerScales, layerScale);

                if (stroke != null)
                {
                    SQ.BoundingBox bbox = stroke.BoundingBox;
                    float sizeX = Mathf.Abs(bbox.MaxX - bbox.MinX);
                    float sizeY = Mathf.Abs(bbox.MaxY - bbox.MinY);
                    float sizeZ = Mathf.Abs(bbox.MaxZ - bbox.MinZ);
                    float diag = Mathf.Sqrt(sizeX * sizeX + sizeY * sizeY + sizeZ * sizeZ);
                    AddSample(_bboxSizeX, sizeX);
                    AddSample(_bboxSizeY, sizeY);
                    AddSample(_bboxSizeZ, sizeZ);
                    AddSample(_bboxDiagonal, diag);

                    if (stroke.Vertices != null)
                    {
                        _vertexCount += stroke.Vertices.Count;
                        foreach (var v in stroke.Vertices)
                        {
                            AddSample(_vertexWidths, v.Width);
                            AddSample(_colorR, v.Color.R);
                            AddSample(_colorG, v.Color.G);
                            AddSample(_colorB, v.Color.B);
                            AddSample(_opacity, v.Opacity);
                        }
                    }

                    if (!string.IsNullOrEmpty(brushGuid))
                    {
                        if (!_brushes.TryGetValue(brushGuid, out var acc))
                        {
                            acc = new BrushAccumulator(brushGuid, stroke.BrushType);
                            _brushes[brushGuid] = acc;
                        }
                        acc.RecordStroke(stroke, strokeMaxWidth, brushSize);
                    }
                }
            }

            public QuillLoadStats ToStats(string path, string kind)
            {
                var stats = new QuillLoadStats();
                stats.Path = path;
                stats.Kind = kind;
                stats.TimestampUtc = DateTime.UtcNow.ToString("o");
                stats.StrokeCount = _strokeMaxWidths.Count;
                stats.VertexCount = _vertexCount;
                stats.VertexWidth = Summarize(_vertexWidths);
                stats.StrokeMaxWidth = Summarize(_strokeMaxWidths);
                stats.StrokeBrushSize = Summarize(_strokeBrushSizes);
                stats.StrokeLayerScale = Summarize(_strokeLayerScales);
                stats.StrokeBoundingBox = new BoundingBoxStats
                {
                    SizeX = Summarize(_bboxSizeX),
                    SizeY = Summarize(_bboxSizeY),
                    SizeZ = Summarize(_bboxSizeZ),
                    Diagonal = Summarize(_bboxDiagonal)
                };
                stats.VertexColor = new ColorChannelStats
                {
                    R = Summarize(_colorR),
                    G = Summarize(_colorG),
                    B = Summarize(_colorB),
                    Opacity = Summarize(_opacity)
                };
                stats.Layers = new List<LayerTransformStats>(_layers);
                var brushStats = new Dictionary<string, BrushStats>(_brushes.Count, StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in _brushes)
                {
                    brushStats[kvp.Key] = kvp.Value.ToStats();
                }
                stats.Brushes = brushStats;
                return stats;
            }

            private static void AddSample(List<float> values, float sample)
            {
                if (values.Count < MaxSamples)
                {
                    values.Add(sample);
                }
            }

            private static FloatStats Summarize(List<float> values)
            {
                var s = new FloatStats();
                if (values == null || values.Count == 0)
                {
                    s.Count = 0;
                    return s;
                }

                values.Sort();
                s.Count = values.Count;
                s.Min = values[0];
                s.Max = values[values.Count - 1];
                s.P50 = values[(int)(0.5f * (values.Count - 1))];
                s.P90 = values[(int)(0.9f * (values.Count - 1))];

                double sum = 0.0;
                for (int i = 0; i < values.Count; i++)
                {
                    sum += values[i];
                }
                s.Mean = (float)(sum / values.Count);
                return s;
            }

            private sealed class BrushAccumulator
            {
                private readonly List<float> _vertexWidths = new List<float>();
                private readonly List<float> _strokeMaxWidths = new List<float>();
                private readonly List<float> _strokeBrushSizes = new List<float>();
                public string BrushGuid { get; }
                public SQ.BrushType BrushType { get; }
                public int StrokeCount { get; private set; }
                public int VertexCount { get; private set; }

                public BrushAccumulator(string guid, SQ.BrushType brushType)
                {
                    BrushGuid = guid;
                    BrushType = brushType;
                }

                public void RecordStroke(SQ.Stroke stroke, float strokeMaxWidth, float brushSize)
                {
                    StrokeCount++;
                    AddSample(_strokeMaxWidths, strokeMaxWidth);
                    AddSample(_strokeBrushSizes, brushSize);

                    if (stroke?.Vertices != null)
                    {
                        VertexCount += stroke.Vertices.Count;
                        foreach (var v in stroke.Vertices)
                        {
                            AddSample(_vertexWidths, v.Width);
                        }
                    }
                }

                public BrushStats ToStats()
                {
                    return new BrushStats
                    {
                        BrushGuid = BrushGuid,
                        BrushType = (int)BrushType,
                        StrokeCount = StrokeCount,
                        VertexCount = VertexCount,
                        VertexWidth = Summarize(_vertexWidths),
                        StrokeMaxWidth = Summarize(_strokeMaxWidths),
                        StrokeBrushSize = Summarize(_strokeBrushSizes)
                    };
                }
            }
        }

        private static QuillStatsRecorder s_QuillStatsRecorder;
    }
}
