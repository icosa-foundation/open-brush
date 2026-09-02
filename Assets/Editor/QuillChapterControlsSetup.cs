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

using TMPro;
using UnityEditor;
using UnityEngine;

namespace TiltBrush
{
    /// <summary>
    /// Editor utility: adds chapter nav controls (prev/next buttons + label) to the
    /// QuillFileButton prefab. Run once from Open Brush > Quill > Add Chapter Controls.
    /// Re-running is safe; existing controls are detected and skipped.
    /// </summary>
    public static class QuillChapterControlsSetup
    {
        private const string k_PrefabPath =
            "Assets/Prefabs/Panels/Widgets/QuillFileButton.prefab";

        // The font used by existing TMP labels in the button (loaded by GUID).
        private const string k_FontGuid = "ec48085d8b1ed18499cf1411d42005a0";

        [MenuItem("Open Brush/Quill/Add Chapter Controls to File Button")]
        public static void Run()
        {
            // Resolve font asset once.
            string fontPath = AssetDatabase.GUIDToAssetPath(k_FontGuid);
            TMP_FontAsset font = string.IsNullOrEmpty(fontPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);

            if (font == null)
            {
                Debug.LogWarning(
                    "QuillChapterControlsSetup: could not resolve font GUID; " +
                    "TMP labels will use the TMP default font.");
            }

            // Load the prefab for in-memory editing.
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(k_PrefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError($"QuillChapterControlsSetup: prefab not found at '{k_PrefabPath}'");
                return;
            }

            try
            {
                var fileButton = prefabRoot.GetComponent<QuillFileButton>();
                if (fileButton == null)
                {
                    Debug.LogError("QuillChapterControlsSetup: QuillFileButton component not found on prefab root.");
                    return;
                }

                // Idempotency check via SerializedObject.
                var soBefore = new SerializedObject(fileButton);
                if (soBefore.FindProperty("m_ChapterControls").objectReferenceValue != null)
                {
                    Debug.Log("QuillChapterControlsSetup: chapter controls already present; skipping.");
                    return;
                }

                int layer = prefabRoot.layer;

                // ── ChapterControls container ──────────────────────────────────────
                // Positioned just below the existing label text (y ≈ -0.04).
                // Starts inactive; RefreshChapterControls() enables it for multi-chapter files.
                var chapterControls = new GameObject("ChapterControls");
                chapterControls.layer = layer;
                chapterControls.transform.SetParent(prefabRoot.transform, false);
                chapterControls.transform.localPosition = new Vector3(0f, -0.043f, -0.003f);
                chapterControls.transform.localRotation = Quaternion.identity;
                chapterControls.transform.localScale = Vector3.one;
                chapterControls.SetActive(false);

                // ── Prev button (<) ────────────────────────────────────────────────
                var prevGO = CreateNavButton(chapterControls.transform,
                    label: "<", isNext: false, layer: layer, font: font);
                prevGO.transform.localPosition = new Vector3(-0.38f, 0f, 0f);

                // ── Chapter label (Ch X / N) ───────────────────────────────────────
                var labelGO = new GameObject("ChapterLabel");
                labelGO.layer = layer;
                labelGO.transform.SetParent(chapterControls.transform, false);
                var labelTmp = labelGO.AddComponent<TextMeshPro>();
                ConfigureTmp(labelTmp, font, fontSize: 5f, text: "Ch - / -",
                    anchoredPos: Vector2.zero, sizeDelta: new Vector2(6f, 1.2f));
                labelGO.transform.localPosition = new Vector3(0f, 0f, 0f);
                labelGO.transform.localScale = Vector3.one * 0.1f;

                // ── Next button (>) ────────────────────────────────────────────────
                var nextGO = CreateNavButton(chapterControls.transform,
                    label: ">", isNext: true, layer: layer, font: font);
                nextGO.transform.localPosition = new Vector3(0.38f, 0f, 0f);

                // ── Wire serialized fields on QuillFileButton ──────────────────────
                var so = new SerializedObject(fileButton);

                so.FindProperty("m_ChapterControls").objectReferenceValue = chapterControls;
                so.FindProperty("m_ChapterLabel").objectReferenceValue = labelTmp;
                so.FindProperty("m_PrevChapterButton")
                    .objectReferenceValue = prevGO.GetComponent<QuillChapterNavButton>();
                so.FindProperty("m_NextChapterButton")
                    .objectReferenceValue = nextGO.GetComponent<QuillChapterNavButton>();

                so.ApplyModifiedProperties();

                // ── Save ───────────────────────────────────────────────────────────
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, k_PrefabPath);
                Debug.Log($"QuillChapterControlsSetup: chapter controls added to '{k_PrefabPath}'.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        // Creates a nav button child GO with BoxCollider, QuillChapterNavButton, and a TMP label.
        private static GameObject CreateNavButton(
            Transform parent, string label, bool isNext, int layer, TMP_FontAsset font)
        {
            // Button root — keep as regular Transform so BoxCollider works cleanly.
            var go = new GameObject(isNext ? "NextChapterButton" : "PrevChapterButton");
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            // BoxCollider for VR raycast interaction.
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(0.18f, 0.03f, 0.05f);
            col.center = Vector3.zero;

            // Chapter nav behaviour.
            var nav = go.AddComponent<QuillChapterNavButton>();
            var soNav = new SerializedObject(nav);
            soNav.FindProperty("m_IsNext").boolValue = isNext;
            // Give it reasonable base-button defaults (no audio, subtle hover).
            soNav.FindProperty("m_ButtonHasPressedAudio").boolValue = true;
            soNav.FindProperty("m_ZAdjustHover").floatValue = -0.005f;
            soNav.FindProperty("m_ZAdjustClick").floatValue = 0.005f;
            soNav.FindProperty("m_HoverScale").floatValue = 1.05f;
            soNav.FindProperty("m_HoverBoxColliderGrow").floatValue = 0.1f;
            soNav.ApplyModifiedProperties();

            // TMP label child (< or >) — child so the button root keeps a plain Transform.
            var labelGO = new GameObject("Label");
            labelGO.layer = layer;
            labelGO.transform.SetParent(go.transform, false);
            var tmp = labelGO.AddComponent<TextMeshPro>();
            ConfigureTmp(tmp, font, fontSize: 6f, text: label,
                anchoredPos: Vector2.zero, sizeDelta: new Vector2(2f, 1.2f));
            labelGO.transform.localScale = Vector3.one * 0.1f;

            return go;
        }

        private static void ConfigureTmp(
            TextMeshPro tmp, TMP_FontAsset font, float fontSize, string text,
            Vector2 anchoredPos, Vector2 sizeDelta)
        {
            // Assigning tmp.font during prefab editing triggers TMP's LoadFontAsset()
            // which can NullRef in Editor context; leave font as TMP default.
            _ = font;
            tmp.fontSize = fontSize;
            tmp.text = text;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;

            var rt = tmp.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            // Local position / rotation come from the parent GO's transform, not the RectTransform.
            rt.localPosition = new Vector3(rt.localPosition.x, rt.localPosition.y, -0.001f);
            rt.localRotation = Quaternion.identity;
        }
    }
}
