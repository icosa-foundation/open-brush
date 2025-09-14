// Copyright 2025 The Open Brush Authors
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

using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace TiltBrush
{
    public class PlayGltfAnimationClip : MonoBehaviour
    {
        private Animation animationComponent;
        private Animator animator; // Keep for debugging/fallback
        private PlayableGraph graph;
        private bool graphInitialized;

        public void PlayAnimation(AnimationClip[] clips)
        {
            if (clips == null || clips.Length == 0) return;

            AnimationClip selectedClip = SelectAnimationClip(clips);
            if (selectedClip == null) return;

            if (selectedClip.legacy)
            {
                PlayLegacyAnimation(selectedClip, clips.Length == 1);
            }
            else
            {
                PlayMecanimAnimation(selectedClip, clips.Length == 1);
            }
        }

        private AnimationClip SelectAnimationClip(AnimationClip[] clips)
        {
            // Single clip: return it (will be auto-looped)
            if (clips.Length == 1)
            {
                return clips[0];
            }

            // Multiple clips: use name-based heuristic to find the most appropriate default
            // Priority order based on common naming patterns from popular 3D software:
            // - "idle": Mixamo and game industry standard for default/rest animations
            // - "animation": Unity's default AnimationClip name
            // - "take 001": Maya/Cinema 4D export pattern
            // - "action": Blender's default Action name
            // - "default"/"base": Generic fallback names
            string[] preferredNames = { "idle", "animation", "take 001", "action", "default", "base" };

            foreach (string preferredName in preferredNames)
            {
                var matchingClip = clips.FirstOrDefault(clip =>
                    clip.name.ToLowerInvariant().Contains(preferredName));
                if (matchingClip != null)
                {
                    return matchingClip;
                }
            }

            // No preferred name found: pick the longest clip
            // Longer animations are often more comprehensive/primary
            var largestClip = clips.OrderByDescending(clip =>
                GetAnimationScore(clip)).FirstOrDefault();

            // Final fallback: use first clip
            return largestClip ?? clips[0];
        }

        private void PlayLegacyAnimation(AnimationClip clip, bool shouldLoop)
        {
            // Remove existing Animator if present (can't have both)
            animator = GetComponent<Animator>();
            if (animator != null)
            {
                DestroyImmediate(animator);
            }

            // Get or create Animation component
            animationComponent = GetComponent<Animation>();
            if (animationComponent == null)
            {
                animationComponent = gameObject.AddComponent<Animation>();
            }

            // Clear existing clips
            animationComponent.Stop();
            foreach (AnimationState state in animationComponent)
            {
                animationComponent.RemoveClip(state.clip);
            }

            // Add and configure the clip
            string clipName = clip.name;
            animationComponent.AddClip(clip, clipName);

            if (shouldLoop)
            {
                animationComponent[clipName].wrapMode = WrapMode.Loop;
            }
            else
            {
                animationComponent[clipName].wrapMode = WrapMode.Once;
            }

            // Play the animation
            animationComponent.Play(clipName);
        }

        private void PlayMecanimAnimation(AnimationClip clip, bool shouldLoop)
        {
            if (graphInitialized && graph.IsValid())
            {
                graph.Destroy();
                graphInitialized = false;
            }

            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = gameObject.AddComponent<Animator>();
            }

            try
            {
                graph = PlayableGraph.Create();
                var playable = AnimationClipPlayable.Create(graph, clip);
                var output = AnimationPlayableOutput.Create(graph, "Animation", animator);

                playable.SetTime(0);
                playable.Play();

                if (shouldLoop)
                {
                    playable.SetDuration(double.PositiveInfinity);
                }

                output.SetSourcePlayable(playable);

                if (graph.IsValid())
                {
                    graph.Play();
                    graphInitialized = true;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to play animation clip: {e.Message}");
                graphInitialized = false;
            }
        }

        private float GetAnimationScore(AnimationClip clip)
        {
            // Use duration as primary complexity indicator since we can't access track count at runtime
            // Longer animations are often more comprehensive/primary
            return clip.length;
        }


        void OnDestroy()
        {
            if (graphInitialized)
            {
                graph.Destroy();
                graphInitialized = false;
            }
        }
    }
}
