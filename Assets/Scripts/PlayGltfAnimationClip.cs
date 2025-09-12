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

// Assets/Scripts/PlayGltfAnimationClip.cs
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

namespace TiltBrush
{
    public class PlayGltfAnimationClip : MonoBehaviour
    {
        private Animator animator;
        private PlayableGraph graph;
        private bool graphInitialized = false;

        public void PlayAnimation(AnimationClip clip)
        {
            if (clip == null) return;

            // Log animation bindings
            var bindings = AnimationUtility.GetCurveBindings(clip);
            Debug.Log($"Animation has {bindings.Length} curve bindings:");
            foreach (var binding in bindings)
            {
                Debug.Log($"  Path: '{binding.path}', Property: '{binding.propertyName}', Type: {binding.type}");
            }

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
                AnimationPlayableUtilities.PlayClip(animator, clip, out graph);

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
