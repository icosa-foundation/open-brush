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

#if OCULUS_SUPPORTED || ZAPBOX_SUPPORTED
#define PASSTHROUGH_SUPPORTED
#endif

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Brush = TiltBrush.BrushDescriptor;

namespace TiltBrush
{

    [System.Serializable]
    public struct BlocksMaterial
    {
        public Brush brushDescriptor;
    }

    public class BrushCatalog : MonoBehaviour
    {
        static public BrushCatalog m_Instance;

#if UNITY_EDITOR
        /// Pass a GameObject to receive the newly-created singleton BrushCatalog
        /// Useful for unit tests because a ton of Tilt Brush uses GetBrush(Guid).
        /// TODO: change TB to use BrushDescriptor directly rather than indirect through Guids
        public static void UnitTestSetUp(GameObject container)
        {
            Debug.Assert(m_Instance == null);
            m_Instance = container.AddComponent<BrushCatalog>();

            // For unit testing, probably best to have all the descriptors available,
            // rather than just a subset of them that are in a manifest.
            m_Instance.m_GuidToBrush = UnityEditor.AssetDatabase.FindAssets("t:BrushDescriptor")
                .Select(name => UnityEditor.AssetDatabase.LoadAssetAtPath<BrushDescriptor>(
                    UnityEditor.AssetDatabase.GUIDToAssetPath(name)))
                .ToDictionary(desc => (Guid)desc.m_Guid);
        }

        /// The inverse of UnitTestSetUp
        public static void UnitTestTearDown(GameObject container)
        {
            Debug.Assert(m_Instance == container.GetComponent<BrushCatalog>());
            m_Instance = null;
        }
#endif

        public event Action BrushCatalogChanged;
        public Texture2D m_GlobalNoiseTexture;

        [SerializeField] private Brush m_DefaultBrush;
        [SerializeField] private Brush m_ZapboxDefaultBrush;
        private bool m_IsLoading;
        private Dictionary<Guid, Brush> m_GuidToBrush;
        private HashSet<Brush> m_AllBrushes;
        private List<Brush> m_GuiBrushList;

        [SerializeField] public BlocksMaterial[] m_BlocksMaterials;
        private Dictionary<Material, Brush> m_MaterialToBrush;

        public bool IsLoading { get { return m_IsLoading; } }
        public Brush GetBrush(Guid guid)
        {
            try
            {
                return m_GuidToBrush[guid];
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }
        public Brush DefaultBrush
        {
            get
            {
#if ZAPBOX_SUPPORTED
                // TODO:Mikesky - Fix brush transparency!
                return m_ZapboxDefaultBrush;
#endif
                return m_DefaultBrush;
            }
        }
        public IEnumerable<Brush> AllBrushes
        {
            get { return m_AllBrushes; }
        }
        public List<Brush> GuiBrushList
        {
            get { return m_GuiBrushList; }
        }

        void Awake()
        {
            m_Instance = this;
            Init();
        }

        public void Init()
        {
            m_GuidToBrush = new Dictionary<Guid, Brush>();
            m_MaterialToBrush = new Dictionary<Material, Brush>();
            m_AllBrushes = new HashSet<Brush>();
            m_GuiBrushList = new List<Brush>();

            // Move blocks materials in to a dictionary for quick lookup.
            for (int i = 0; i < m_BlocksMaterials.Length; ++i)
            {
                m_MaterialToBrush.Add(m_BlocksMaterials[i].brushDescriptor.Material,
                    m_BlocksMaterials[i].brushDescriptor);
            }
            Shader.SetGlobalTexture("_GlobalNoiseTexture", m_GlobalNoiseTexture);
        }

        /// Begins reloading any brush assets that come from loose files.
        /// The "BrushCatalogChanged" event will be fired when this is complete.
        public void BeginReload()
        {
            m_IsLoading = true;

            // Recreate m_GuidToBrush
            {
                var manifestBrushes = LoadBrushesInManifest();
                manifestBrushes.Add(DefaultBrush);

                m_GuidToBrush.Clear();
                m_AllBrushes = null;

                foreach (var brush in manifestBrushes)
                {
                    Brush tmp;
                    if (m_GuidToBrush.TryGetValue(brush.m_Guid, out tmp) && tmp != brush)
                    {
                        Debug.LogErrorFormat("Guid collision: {0}, {1}", tmp, brush);
                        continue;
                    }
                    m_GuidToBrush[brush.m_Guid] = brush;
                }

                // Add reverse links to the brushes
                // Auto-add brushes as compat brushes
                foreach (var brush in manifestBrushes) { brush.m_SupersededBy = null; }
                foreach (var brush in manifestBrushes)
                {
                    var older = brush.m_Supersedes;
                    if (older == null) { continue; }
                    // Add as compat
                    if (!m_GuidToBrush.ContainsKey(older.m_Guid))
                    {
                        m_GuidToBrush[older.m_Guid] = older;
                        older.m_HiddenInGui = true;
                    }
                    // Set reverse link
                    if (older.m_SupersededBy != null)
                    {
                        // No need to warn if the superseding brush is the same
                        if (older.m_SupersededBy.name != brush.name)
                        {
                            Debug.LogWarningFormat(
                                "Unexpected: {0} is superseded by both {1} and {2}",
                                older.name, older.m_SupersededBy.name, brush.name);
                        }
                    }
                    else
                    {
                        older.m_SupersededBy = brush;
                    }
                }

                m_AllBrushes = new HashSet<Brush>(m_GuidToBrush.Values);
            }

            // Postprocess: put brushes into parse-friendly list
            m_GuiBrushList.Clear();
            foreach (var brush in m_GuidToBrush.Values)
            {
                if (brush.m_HiddenInGui)
                {
                    continue;
                }
                m_GuiBrushList.Add(brush);
            }
        }


        public Brush[] GetTagFilteredBrushList()
        {
            List<string> includeTags = App.UserConfig.Brushes.IncludeTags.ToList();
            List<string> excludeTags = App.UserConfig.Brushes.ExcludeTags.ToList();

            if (includeTags == null || includeTags.Count == 0)
            {
                Debug.LogError("There will be no brushes because there are no 'include' tags.");
            }

#if !PASSTHROUGH_SUPPORTED
            excludeTags.Add("passthrough");
#endif

            // Filter m_GuiBrushList down to those that are both 'included' and not 'excluded'
            Brush[] filteredList = m_GuiBrushList.Where((brush) =>
            {
                // Is this brush excluded?
                bool? excluded = excludeTags?.Intersect(brush.m_Tags).Any();
                if (excluded == true || includeTags == null || brush.m_Tags.Contains("broken"))
                {
                    return false;
                }

                // Is this brush included?
                return includeTags.Intersect(brush.m_Tags).Any();
            }).ToArray();

            return filteredList;
        }

        void Update()
        {
            if (m_IsLoading)
            {
                m_IsLoading = false;
                Resources.UnloadUnusedAssets();
                ModifyBrushTags();
                BrushCatalogChanged?.Invoke();
            }
        }
        private void ModifyBrushTags()
        {
            Dictionary<string, string[]> tagsToAddMap = App.UserConfig.Brushes.AddTagsToBrushes;
            Dictionary<string, string[]> tagsToRemoveMap = App.UserConfig.Brushes.RemoveTagsFromBrushes;

            // Add tags
            foreach (KeyValuePair<string, string[]> brushTagsPair in tagsToAddMap)
            {
                Brush brush = _FindBrushByDescription(brushTagsPair.Key);
                if (brush)
                {
                    string[] tagsToAdd = brushTagsPair.Value;
                    brush.m_Tags.AddRange(tagsToAdd);
                    brush.m_Tags = brush.m_Tags.Distinct().ToList();
                }
                else
                {
                    Debug.LogError($"Could not find brush ({brushTagsPair.Key}) to add tags to");
                }
            }

            // Remove tags
            foreach (KeyValuePair<string, string[]> brushTagsPair in tagsToRemoveMap)
            {
                Brush brush = _FindBrushByDescription(brushTagsPair.Key);
                if (brush)
                {
                    string[] tagsToRemove = brushTagsPair.Value;
                    brush.m_Tags = brush.m_Tags.Except(tagsToRemove).ToList();
                }
                else
                {
                    Debug.LogError($"Could not find brush ({brushTagsPair.Key}) to remove tags from");
                }
            }

            Brush _FindBrushByDescription(string brushDescription)
            {
                string searchString = brushDescription.Trim();
                StringComparison comparison = StringComparison.CurrentCultureIgnoreCase;
                return m_AllBrushes.FirstOrDefault(descriptor => descriptor.Description.Equals(searchString, comparison));
            }
        }

        // Returns brushes in both sections of the manifest (compat and non-compat)
        // Brushes that are found only in the compat section will have m_HiddenInGui = true
        static private List<Brush> LoadBrushesInManifest()
        {
            List<Brush> output = new List<Brush>();
            var manifest = App.Instance.m_Manifest;
            foreach (var desc in manifest.Brushes)
            {
                if (desc != null)
                {
                    output.Add(desc);
                }
            }

            // Additional hidden brushes
            var hidden = manifest.CompatibilityBrushes.Except(manifest.Brushes);
            foreach (var desc in hidden)
            {
                if (desc != null)
                {
                    desc.m_HiddenInGui = true;
                    output.Add(desc);
                }
            }
            return output;
        }
    }
} // namespace TiltBrush
