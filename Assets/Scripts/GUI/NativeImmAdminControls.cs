// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using TMPro;
using UnityEngine;

namespace TiltBrush
{
    /// <summary>Scene playback controls below the admin panel, using the nested tray UI path.</summary>
    [RequireComponent(typeof(UIComponentManager), typeof(BoxCollider))]
    public class NativeImmAdminControls : UIComponent
    {
        [SerializeField] private GameObject m_Content;
        [SerializeField] private GameObject m_ViewpointControls;
        [SerializeField] private GameObject m_ChapterControls;
        [SerializeField] private TMP_Text m_ViewpointLabel;
        [SerializeField] private TMP_Text m_ChapterLabel;
        [SerializeField] private QuillChapterNavButton m_PlaybackButton;
        [SerializeField] private QuillChapterNavButton m_PrevViewpointButton;
        [SerializeField] private QuillChapterNavButton m_NextViewpointButton;
        [SerializeField] private QuillChapterNavButton m_PrevChapterButton;
        [SerializeField] private QuillChapterNavButton m_NextChapterButton;
        [SerializeField] private Transform m_Frame;
        [SerializeField] private Renderer m_FrameRenderer;
        [SerializeField] private Vector2 m_SingleRowSize = new Vector2(1.5f, 0.34f);
        [SerializeField] private float m_ChapterRowOffset = 0.33f;
        [SerializeField] private Vector3 m_PlaybackWithViewpointsPosition = new Vector3(-0.55f, 0, -0.05f);
        [SerializeField] private Vector3 m_PlaybackOnlyPosition = new Vector3(0, 0, -0.05f);

        private UIComponentManager m_ChildManager;

        protected override void Awake()
        {
            base.Awake();
            m_ChildManager = GetComponent<UIComponentManager>();
        }

        private void LateUpdate()
        {
            bool visible = NativeImmImport.IsActive && App.VrSdk != null &&
                App.VrSdk.IsHmdInitialized() && App.Config.m_SdkMode != SdkMode.Monoscopic;
            if (m_Content.activeSelf != visible)
            {
                m_ChildManager.Deactivate();
                m_Content.SetActive(visible);
            }
            m_Collider.enabled = visible;
            if (!visible) return;

            int viewpointCount = NativeImmImport.ViewpointCount;
            bool viewpoints = viewpointCount > 1;
            bool chapters = NativeImmImport.ChapterCount > 1;
            UpdateLayout(viewpoints, chapters);

            bool ready = !NativeImmImport.IsLoading && !NativeImmImport.IsChangingChapter;
            m_PrevViewpointButton.SetButtonAvailable(ready && viewpoints);
            m_NextViewpointButton.SetButtonAvailable(ready && viewpoints);
            m_PrevChapterButton.SetButtonAvailable(ready && chapters);
            m_NextChapterButton.SetButtonAvailable(ready && chapters);
            m_PlaybackButton.RefreshPlaybackState();

            int viewpoint = NativeImmImport.CurrentViewpointIndex;
            m_ViewpointLabel.text = viewpoint >= 0
                ? $"Viewpoint {viewpoint + 1} / {viewpointCount}"
                : $"Viewpoint — / {viewpointCount}";
            m_ChapterLabel.text = $"Chapter {(NativeImmImport.CurrentChapterIndex ?? 0) + 1} / {NativeImmImport.ChapterCount}";
        }

        private void UpdateLayout(bool viewpoints, bool chapters)
        {
            SetGroupVisible(m_ViewpointControls, viewpoints);
            SetGroupVisible(m_ChapterControls, chapters);
            m_PlaybackButton.transform.localPosition = viewpoints
                ? m_PlaybackWithViewpointsPosition : m_PlaybackOnlyPosition;

            // One layout drives both the visible frame and the extension's input collider.
            float extraHeight = chapters ? m_ChapterRowOffset : 0;
            var size = new Vector2(viewpoints || chapters ? m_SingleRowSize.x : m_SingleRowSize.y,
                m_SingleRowSize.y + extraHeight);
            var center = new Vector3(0, -extraHeight * 0.5f, 0);
            var collider = (BoxCollider)m_Collider;
            collider.size = new Vector3(size.x, size.y, 0.12f);
            collider.center = center;
            m_Frame.localPosition = center;
            m_Frame.localScale = new Vector3(size.x, size.y, 1);
        }

        private void SetGroupVisible(GameObject group, bool visible)
        {
            if (group.activeSelf == visible) return;
            m_ChildManager.Deactivate();
            group.SetActive(visible);
        }

        public override void SetColor(Color color)
        {
            base.SetColor(color);
            m_ChildManager.SetColor(color);
            m_ViewpointLabel.color = color;
            m_ChapterLabel.color = color;
            m_FrameRenderer.material.SetColor("_Color", color);
        }

        public override void UpdateVisuals()
        {
            base.UpdateVisuals();
            m_ChildManager.UpdateVisuals();
        }

        public override bool UpdateStateWithInput(bool inputValid, Ray inputRay,
            GameObject parentActiveObject, Collider parentCollider)
        {
            if (!m_Content.activeInHierarchy) return false;
            if (base.UpdateStateWithInput(inputValid, inputRay, parentActiveObject, parentCollider) &&
                (parentActiveObject == null || parentActiveObject == gameObject) &&
                BasePanel.DoesRayHitCollider(inputRay, GetCollider()))
            {
                m_ChildManager.UpdateUIComponents(inputRay, inputValid, parentCollider);
                return true;
            }
            return false;
        }

        public override bool RaycastAgainstCustomCollider(Ray ray, out RaycastHit hitInfo, float dist)
        {
            hitInfo = default;
            return m_Content.activeInHierarchy && m_Collider.enabled &&
                m_Collider.Raycast(ray, out hitInfo, dist);
        }

        public override bool CalculateReticleCollision(Ray ray, ref Vector3 pos, ref Vector3 forward)
        {
            if (!RaycastAgainstCustomCollider(ray, out var hitInfo, 100f)) return false;
            pos = hitInfo.point;
            forward = hitInfo.normal;
            m_ChildManager.CalculateReticleCollision(ray, ref pos, ref forward);
            return true;
        }

        public override void ResetState()
        {
            base.ResetState();
            m_ChildManager.Deactivate();
        }
    }
}
