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

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;

namespace TiltBrush
{
    public enum OverlayType
    {
        LoadSketch,
        LoadModel,
        LoadGeneric,
        LoadImages,
        Export,
        LoadMedia,
    }

    public enum OverlayState
    {
        Hidden,
        Visible,
        Exiting
    }

    public class OverlayManager : MonoBehaviour
    {
        public static OverlayManager m_Instance;

        [SerializeField] private GvrOverlay m_GvrOverlayPrefab;

        [SerializeField] private float m_OverlayOffsetDistance;
        [SerializeField] private float m_OverlayHeight;
        [SerializeField] private Texture m_BlackTexture;
        [SerializeField] private float m_OverlayStateTransitionDuration = 1.0f;

        [SerializeField] private Material m_Material;
        [SerializeField] private Font m_Font;
        [SerializeField] private Rect m_LogoArea;
        [SerializeField] private Rect m_TextArea;
        [SerializeField] private int m_FontSize;
        [SerializeField] private int m_Size;
        [SerializeField] private Color m_BackgroundColor;
        [SerializeField] private Color m_TextColor;

        [SerializeField] private LocalizedString m_LoadSketchText;
        [SerializeField] private LocalizedString m_LoadModelText;
        [SerializeField] private LocalizedString m_LoadGenericText;
        [SerializeField] private LocalizedString m_LoadImagesText;
        [SerializeField] private LocalizedString m_ExportText;
        [SerializeField] private LocalizedString m_LoadMediaText;
        [SerializeField] private LocalizedString m_SuccessText;

        private enum OverlayMode
        {
            None,
            Default
        }

        //Overlay
        private GvrOverlay m_Overlay;
        private OverlayMode m_OverlayMode = OverlayMode.None;
        private bool m_OverlayOn;

        private Progress<double> m_progress;
        private RenderTexture m_GUILogo;
        private bool m_RefuseProgressChanges;
        private OverlayState m_CurrentOverlayState;
        private float m_OverlayStateTransitionValue;
        private OverlayType m_CurrentOverlayType;

        private Vector3[] m_TextVertices;
        private Vector3[] m_TextUvs;


        /// An IProgress for you to use with your RunInCompositor tasks/coroutines
        public IProgress<double> Progress => m_progress;

        public bool CanDisplayQuickloadOverlay
        {
            get { return !OverlayEnabled || m_CurrentOverlayType == OverlayType.LoadSketch; }
        }

        public OverlayState CurrentOverlayState => m_CurrentOverlayState;

        void Awake()
        {
            m_Instance = this;
            m_progress = new Progress<double>();
            m_progress.ProgressChanged += OnProgressChanged;
            m_CurrentOverlayState = OverlayState.Hidden;
            m_OverlayStateTransitionValue = 0.0f;
            m_GUILogo = new RenderTexture(m_Size, m_Size, 0);
            SetText("");
            RenderLogo(0.45f);

            if (m_GvrOverlayPrefab != null)
            {
                m_OverlayMode = OverlayMode.Default;
                m_Overlay = Instantiate(m_GvrOverlayPrefab);
                m_Overlay.gameObject.SetActive(false);
            }
        }

        void Update()
        {
            switch (m_CurrentOverlayState)
            {
                case OverlayState.Exiting:
                    m_OverlayStateTransitionValue -= Time.deltaTime;
                    SetOverlayAlpha(
                        Mathf.Max(m_OverlayStateTransitionValue, 0.0f) / m_OverlayStateTransitionDuration);
                    if (m_OverlayStateTransitionValue <= 0.0f)
                    {
                        m_OverlayStateTransitionValue = 0.0f;
                        m_CurrentOverlayState = OverlayState.Hidden;
                        OverlayEnabled = false;
                    }
                    break;
                case OverlayState.Hidden:
                case OverlayState.Visible:
                default:
                    break;
            }
        }

        // -------------------------------------------------------------------------------------------- //
        // Overlay Methods
        // (Moved from VrSdk)
        // -------------------------------------------------------------------------------------------- //
        private void SetOverlayAlpha(float ratio)
        {
            switch (m_OverlayMode)
            {
                case OverlayMode.Default:
                    if (!OverlayEnabled && ratio > 0.0f)
                    {
                        // Position screen overlay in front of the camera.
                        m_Overlay.transform.parent = App.VrSdk.GetVrCamera().transform;
                        m_Overlay.transform.localPosition = Vector3.zero;
                        m_Overlay.transform.localRotation = Quaternion.identity;
                        float scale = 0.5f * App.VrSdk.GetVrCamera().farClipPlane / App.VrSdk.GetVrCamera().transform.lossyScale.z;
                        m_Overlay.transform.localScale = Vector3.one * scale;

                        // Reparent the overlay so that it doesn't move with the headset.
                        m_Overlay.transform.parent = null;

                        // Reset the rotation so that it's level and centered on the horizon.
                        Vector3 eulerAngles = m_Overlay.transform.localRotation.eulerAngles;
                        m_Overlay.transform.localRotation = Quaternion.Euler(new Vector3(0, eulerAngles.y, 0));

                        m_Overlay.gameObject.SetActive(true);
                        OverlayEnabled = true;
                    }
                    else if (OverlayEnabled && ratio == 0.0f)
                    {
                        m_Overlay.gameObject.SetActive(false);
                        OverlayEnabled = false;
                    }
                    break;
            }
        }

        public bool OverlayEnabled
        {
            get
            {
                switch (m_OverlayMode)
                {
                    case OverlayMode.Default:
                        return m_OverlayOn;
                    default:
                        return false;
                }
            }
            set
            {
                switch (m_OverlayMode)
                {
                    case OverlayMode.Default:
                        m_OverlayOn = value;
                        break;
                }
            }
        }

        private void SetOverlayTexture(Texture tex)
        {
            switch (m_OverlayMode)
            {
                default:
                    break;
            }
        }

        private void PositionOverlay(float distance, float height)
        {
            //place overlay in front of the player a distance out
            Vector3 vOverlayPosition = ViewpointScript.Head.position;
            Vector3 vOverlayDirection = ViewpointScript.Head.forward;
            vOverlayDirection.y = 0.0f;
            vOverlayDirection.Normalize();

            switch (m_OverlayMode)
            {
                default:
                    break;
            }
        }

        // Fades to the compositor world (if available) or black.
        public void FadeToCompositor(float fadeTime)
        {
            FadeToCompositor(fadeTime, fadeToCompositor: true);
        }

        // Fades from the compositor world (if available) or black.
        public void FadeFromCompositor(float fadeTime)
        {
            FadeToCompositor(fadeTime, fadeToCompositor: false);
        }

        private void FadeToCompositor(float fadeTime, bool fadeToCompositor)
        {
            switch (m_OverlayMode)
            {
                default:
                    break;
            }
        }

        public void PauseRendering(bool bPause)
        {
            switch (m_OverlayMode)
            {
                default:
                    break;
            }
        }

        // Fades to solid black.
        private void FadeToBlack(float fadeTime)
        {
            FadeBlack(fadeTime, fadeToBlack: true);
        }

        // Fade from solid black.
        private void FadeFromBlack(float fadeTime)
        {
            FadeBlack(fadeTime, fadeToBlack: false);
        }

        private void FadeBlack(float fadeTime, bool fadeToBlack)
        {

            // TODO: using Viewpoint here is pretty gross, dependencies should not go from VrSdk
            // to other Open Brush components.

            // Currently ViewpointScript.FadeToColor takes 1/time as a parameter, which we should fix to
            // make consistent, but for now just convert the incoming parameter.
            float speed = 1 / Mathf.Max(fadeTime, 0.00001f);
            if (fadeToBlack)
            {
                ViewpointScript.m_Instance.FadeToColor(Color.black, speed);
            }
            else
            {
                ViewpointScript.m_Instance.FadeToScene(speed);
            }
        }

        void OnGUI()
        {
            if (OverlayEnabled)
            {
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), m_BlackTexture);
                GUI.DrawTexture(new Rect(Screen.width / 2 - Screen.height / 4, Screen.height / 4,
                    Screen.height / 2, Screen.height / 2), m_GUILogo);
            }
        }

        public void SetOverlayFromType(OverlayType type)
        {
            m_CurrentOverlayType = type;
            switch (type)
            {
                case OverlayType.LoadSketch:
                    SetText(m_LoadSketchText.GetLocalizedStringAsync().Result);
                    RenderLogo(0);
                    SetOverlayTexture(m_GUILogo);
                    break;
                case OverlayType.LoadModel:
                    SetText(m_LoadModelText.GetLocalizedStringAsync().Result);
                    RenderLogo(0);
                    SetOverlayTexture(m_GUILogo);
                    break;
                case OverlayType.LoadGeneric:
                    SetText(m_LoadGenericText.GetLocalizedStringAsync().Result);
                    RenderLogo(0);
                    SetOverlayTexture(m_GUILogo);
                    break;
                case OverlayType.LoadImages:
                    SetText(m_LoadImagesText.GetLocalizedStringAsync().Result);
                    RenderLogo(0);
                    SetOverlayTexture(m_GUILogo);
                    break;
                case OverlayType.Export:
                    SetText(m_ExportText.GetLocalizedStringAsync().Result);
                    RenderLogo(0);
                    SetOverlayTexture(m_GUILogo);
                    break;
                case OverlayType.LoadMedia:
                    SetText(m_LoadMediaText.GetLocalizedStringAsync().Result);
                    RenderLogo(0);
                    SetOverlayTexture(m_GUILogo);
                    break;
            }
        }

        // This is used when we pass m_progress to a Task
        // It is guaranteed to be called on the Unity thread.
        private void OnProgressChanged(object sender, double value)
        {
            UpdateProgress((float)value);
        }

        public void UpdateProgress(float fProg, bool bForce = false)
        {
            if (m_RefuseProgressChanges && !bForce) { return; }
            RenderLogo(fProg);
        }

        public void RefuseProgressBarChanges(bool bRefuse)
        {
            m_RefuseProgressChanges = bRefuse;
        }

        /// Like RunInCompositor, but allows you to pass an IEnumerator(float).
        /// The float being yielded should be a progress value between 0 and 1.
        public IEnumerator<Null> RunInCompositorWithProgress(
            OverlayType overlayType,
            IEnumerator<float> coroutineWithProgress,
            float fadeDuration)
        {
            return RunInCompositor(
                overlayType,
                ConsumeAsProgress(coroutineWithProgress),
                fadeDuration,
                false);
        }

        /// Like RunInCompositor but for non-coroutines
        public IEnumerator<Null> RunInCompositor(
            OverlayType overlayType,
            System.Action action,
            float fadeDuration,
            bool bFullProgress = false,
            bool showSuccessText = false)
        {
            return RunInCompositor(overlayType, AsCoroutine(action), fadeDuration,
                bFullProgress, showSuccessText);
        }

        public IEnumerator<Null> RunInCompositor(
            OverlayType overlayType,
            IEnumerator<Null> action,
            float fadeDuration,
            bool bFullProgress = false,
            bool showSuccessText = false)
        {
            SetOverlayFromType(overlayType);
            UpdateProgress(bFullProgress ? 1.0f : 0.0f);

            SetOverlayAlpha(0);
            yield return null;

            bool routineInterrupted = true;
            try
            {
                FadeToCompositor(fadeDuration);
                // You can't rely on the SteamVR compositor fade being totally over in the time
                // you specified. You also can't rely on being able to get a sensible value for the fade
                // alpha, so you can't reliably wait for it to be done.
                // Therefore, we use the simple method of just waiting a bit longer than we should
                // need to.
                for (float t = 0; t < 1.1f; t += Time.deltaTime / fadeDuration)
                {
                    SetOverlayTransitionRatio(Mathf.Clamp01(t));
                    yield return null;
                }

                // Wait one additional frame for any transitions to complete (e.g. fade to black).
                SetOverlayTransitionRatio(1.0f);
                PauseRendering(true);
                yield return null;

                try
                {
                    while (true)
                    {
                        try
                        {
                            if (!action.MoveNext())
                            {
                                break;
                            }
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogException(e);
                            break;
                        }
                        yield return action.Current;
                    }
                }
                finally
                {
                    action.Dispose();
                }
                yield return null; // eat a frame
                if (showSuccessText)
                {
                    SetText(m_SuccessText.GetLocalizedStringAsync().Result);
                    float successHold = 1.0f;
                    while (successHold >= 0.0f)
                    {
                        successHold -= Time.deltaTime;
                        yield return null;
                    }
                }

                PauseRendering(false);
                FadeFromCompositor(fadeDuration);
                for (float t = 1; t > 0; t -= Time.deltaTime / fadeDuration)
                {
                    SetOverlayTransitionRatio(Mathf.Clamp01(t));
                    yield return null;
                }
                SetOverlayTransitionRatio(0);
                routineInterrupted = false;
            }
            finally
            {
                if (routineInterrupted)
                {
                    // If the coroutine was interrupted, clean up our compositor fade.
                    PauseRendering(false);
                    FadeFromCompositor(0.0f);
                    SetOverlayTransitionRatio(0.0f);
                }
            }
        }

        // Start or end are normally in [0, 1] but can be slightly greater if you want some room
        // to account for SteamVR latency.
        private async Task FadeCompositorAndOverlayAsync(float start, float end, float duration)
        {
            if (end > start) { FadeToCompositor(duration); }
            else { FadeFromCompositor(duration); }

            for (float elapsed = 0; elapsed < duration; elapsed += Time.deltaTime)
            {
                float cur = Mathf.Lerp(start, end, elapsed / duration);
                SetOverlayTransitionRatio(Mathf.Clamp01(cur));
                await Awaiters.NextFrame;
            }
            SetOverlayTransitionRatio(Mathf.Clamp01(end));
        }

        /// Does some async work inside the compositor.
        /// It's assumed that the work will yield often enough that it makes sense to
        /// provide an IProgress callback for the Task.
        /// Like the Coroutine-based overloads, the work is only started after the
        /// fade-to-compositor completes.
        public async Task<T> RunInCompositorAsync<T>(
            OverlayType overlayType,
            Func<IProgress<double>, Task<T>> taskCreator,
            float fadeDuration,
            bool showSuccessText = false)
        {
            SetOverlayFromType(overlayType);
            bool bFullProgress = false;
            UpdateProgress(bFullProgress ? 1.0f : 0.0f);

            SetOverlayAlpha(0);
            await Awaiters.NextFrame;

            try
            {
                // You can't rely on the SteamVR compositor fade being totally over in the time
                // you specified. You also can't rely on being able to get a sensible value for the fade
                // alpha, so you can't reliably wait for it to be done.
                // Therefore, we use the simple method of just waiting a bit longer than we should
                // need to, by passing slightly-too-wide bounds.
                await FadeCompositorAndOverlayAsync(0, 1.1f, fadeDuration);
                // Wait one additional frame for any transitions to complete (e.g. fade to black).
                PauseRendering(true);
                await Awaiters.NextFrame;

                Task<T> inner = taskCreator(m_progress);
                try
                {
                    await inner;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                if (showSuccessText)
                {
                    SetText(m_SuccessText.GetLocalizedStringAsync().Result);
                    await Awaiters.Seconds(1f);
                }

                PauseRendering(false);
                await FadeCompositorAndOverlayAsync(1, 0, fadeDuration);
                return inner.Result;
            }
            catch (Exception)
            {
                PauseRendering(false);
                FadeFromCompositor(0);
                SetOverlayTransitionRatio(0);
                throw;
            }
        }

        /// Does some synchronous work inside the compositor.
        /// Some mostly-faked progress will be displayed.
        public async Task<T> RunInCompositorAsync<T>(
            OverlayType overlayType,
            Func<T> action,
            float fadeDuration,
            bool showSuccessText = false)
        {
            SetOverlayFromType(overlayType);
            bool bFullProgress = false;
            UpdateProgress(bFullProgress ? 1.0f : 0.0f);

            SetOverlayAlpha(0);
            await Awaiters.NextFrame;

            try
            {
                // You can't rely on the SteamVR compositor fade being totally over in the time
                // you specified. You also can't rely on being able to get a sensible value for the fade
                // alpha, so you can't reliably wait for it to be done.
                // Therefore, we use the simple method of just waiting a bit longer than we should
                // need to, by passing slightly-too-wide bounds.
                await FadeCompositorAndOverlayAsync(0, 1.1f, fadeDuration);
                // Wait one additional frame for any transitions to complete (e.g. fade to black).
                PauseRendering(true);
                Progress.Report(0.25);
                await Awaiters.NextFrame;

                T result = default;
                try
                {
                    result = action();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                Progress.Report(0.75);
                if (showSuccessText)
                {
                    SetText(m_SuccessText.GetLocalizedStringAsync().Result);
                    await Awaiters.Seconds(1f);
                }

                PauseRendering(false);
                await FadeCompositorAndOverlayAsync(1, 0, fadeDuration);
                return result;
            }
            catch (Exception)
            {
                PauseRendering(false);
                FadeFromCompositor(0);
                SetOverlayTransitionRatio(0);
                throw;
            }
        }

        public void SetOverlayTransitionRatio(float fRatio)
        {
            m_OverlayStateTransitionValue = m_OverlayStateTransitionDuration * fRatio;
            bool overlayWasActive = OverlayEnabled;
            SetOverlayAlpha(fRatio);
            if (!overlayWasActive && OverlayEnabled)
            {
                PositionOverlay(m_OverlayOffsetDistance, m_OverlayHeight);
            }
            m_CurrentOverlayState = OverlayState.Visible;
        }

        public void HideOverlay()
        {
            if (m_CurrentOverlayState == OverlayState.Visible)
            {
                m_CurrentOverlayState = OverlayState.Exiting;
            }
        }

        private static IEnumerator<Null> AsCoroutine(System.Action action)
        {
            action();
            yield break;
        }

        private IEnumerator<Null> ConsumeAsProgress(IEnumerator<float> coroutine)
        {
            using (var coroutineWithProgress = coroutine)
            {
                while (coroutineWithProgress.MoveNext())
                {
                    UpdateProgress(coroutineWithProgress.Current);
                    yield return null;
                }
            }
        }

        public void RenderLogo(double progress)
        {
            // TODO:Mikesky Temp hack to set correct logo progress
            if (m_OverlayMode == OverlayMode.Default)
            {
                m_Overlay.GetComponent<GvrOverlay>().Progress = (float)progress;
                return;
            }

            // TODO:Mikesky Old code which is generating an image, then submitting to platform specific compositor.
            RenderTexture.active = m_GUILogo;
            GL.Clear(true, true, m_BackgroundColor);
            GL.PushMatrix();
            GL.LoadOrtho();
            m_Material.SetFloat("_Progress", (float)progress);
            m_Material.SetPass(0);
            GL.Begin(GL.QUADS);
            GL.Color(Color.white);
            GL.TexCoord2(0f, 1f);
            GL.Vertex3(m_LogoArea.xMin, m_LogoArea.yMax, 0);
            GL.TexCoord2(1f, 1f);
            GL.Vertex3(m_LogoArea.xMax, m_LogoArea.yMax, 0);
            GL.TexCoord2(1f, 0f);
            GL.Vertex3(m_LogoArea.xMax, m_LogoArea.yMin, 0);
            GL.TexCoord2(0f, 0f);
            GL.Vertex3(m_LogoArea.xMin, m_LogoArea.yMin, 0);
            GL.End();
            m_Font.material.SetPass(0);
            GL.Begin(GL.QUADS);
            for (int i = 0; i < m_TextVertices.Length; ++i)
            {
                GL.TexCoord(m_TextUvs[i]);
                GL.Vertex(m_TextVertices[i]);
            }
            GL.End();
            GL.PopMatrix();
            RenderTexture.active = null;
        }

        public void SetText(string text)
        {
            // TODO:Mikesky Temp hack to set correct logo progress
            if (m_OverlayMode == OverlayMode.Default)
            {
                m_Overlay.GetComponent<GvrOverlay>().MessageStatus = text;
                return;
            }

            // TODO:Mikesky Old code which is generating text glyphs, then submitting to compositor
            var settings = new TextGenerationSettings();
            settings.font = m_Font;
            settings.color = m_TextColor;
            settings.generationExtents = Vector2.one * 1000f;
            settings.resizeTextForBestFit = true;
            settings.textAnchor = TextAnchor.MiddleCenter;
            settings.fontSize = m_FontSize;
            settings.fontStyle = FontStyle.Normal;
            settings.scaleFactor = 1f;
            settings.generateOutOfBounds = true;
            settings.horizontalOverflow = HorizontalWrapMode.Overflow;
            settings.resizeTextMaxSize = m_FontSize;
            settings.resizeTextMinSize = m_FontSize;

            var generator = new TextGenerator();
            if (generator.Populate(text, settings))
            {
                var vertices = generator.GetVerticesArray();
                m_TextVertices = new Vector3[vertices.Length];
                m_TextUvs = new Vector3[vertices.Length];

                int index = 0;
                foreach (var vertex in vertices)
                {
                    Vector3 position = vertex.position - new Vector3(500.0f, 500.0f, 0);
                    position *= m_TextArea.height / m_FontSize * 0.8f;
                    position += new Vector3(m_TextArea.center.x, m_TextArea.center.y, 0f);
                    m_TextVertices[index] = position;
                    m_TextUvs[index] = new Vector3(vertex.uv0.x, vertex.uv0.y, 0);
                    ++index;
                }
            }
        }
    }
} // namespace TiltBrush
