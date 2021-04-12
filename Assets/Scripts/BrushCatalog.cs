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
using System.IO;
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
            m_Instance.m_BuiltinBrushes = UnityEditor.AssetDatabase.FindAssets("t:BrushDescriptor")
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
        private bool m_CatalogChanged;
        private List<Brush> m_GuiBrushList;

        private Dictionary<Guid, Brush> m_BuiltinBrushes;
        private Dictionary<Guid, Brush> m_LibraryBrushes;
        private Dictionary<Guid, Brush> m_SceneBrushes;

        private List<string> m_ChangedBrushes;

        [SerializeField] public BlocksMaterial[] m_BlocksMaterials;
        private Dictionary<Material, Brush> m_MaterialToBrush;

        private FileWatcher m_FileWatcher;

        public bool IsLoading { get { return m_CatalogChanged; } }

        /// <summary>
        /// GetBrush Looks in the following places for brushes, in order:
        /// 1) Built-in brushes
        /// 2) Brushes in the Brush Library
        /// 3) Brushes in the Scene.
        /// </summary>
        /// <param name="guid">Guid of the brush to seach for.</param>
        /// <returns>The brush, if it can be found. Otherwise, null.</returns>
        public Brush GetBrush(Guid guid)
        {
            Brush brush;
            if (m_BuiltinBrushes.TryGetValue(guid, out brush))
            {
                return brush;
            }
            if (m_LibraryBrushes.TryGetValue(guid, out brush))
            {
                return brush;
            }
            if (m_SceneBrushes.TryGetValue(guid, out brush))
            {
                return brush;
            }
            return null;
        }
        public Brush DefaultBrush => m_DefaultBrush;

        public IEnumerable<Brush> AllBrushes => KeepOrderDistinct(
          m_BuiltinBrushes.Values.Concat(m_LibraryBrushes.Values.Concat(m_SceneBrushes.Values)));

        public List<Brush> GuiBrushList => m_GuiBrushList;

        public bool IsBrushBuiltIn(BrushDescriptor brush)
        {
            return m_BuiltinBrushes.ContainsKey(brush.m_Guid);
        }
        public bool IsBrushInLibrary(BrushDescriptor brush)
        {
            return !m_BuiltinBrushes.ContainsKey(brush.m_Guid) &&
                   m_LibraryBrushes.ContainsKey(brush.m_Guid);
        }

        public bool IsBrushInSketch(BrushDescriptor brush)
        {
            return !m_BuiltinBrushes.ContainsKey(brush.m_Guid) &&
                   !m_LibraryBrushes.ContainsKey(brush.m_Guid) &&
                   m_SceneBrushes.ContainsKey(brush.m_Guid);
        }

        void Awake()
        {
            m_Instance = this;
            m_BuiltinBrushes = new Dictionary<Guid, Brush>();
            m_LibraryBrushes = new Dictionary<Guid, Brush>();
            m_SceneBrushes = new Dictionary<Guid, Brush>();
            m_MaterialToBrush = new Dictionary<Material, Brush>();
            m_ChangedBrushes = new List<string>();

            // Move blocks materials in to a dictionary for quick lookup.
            for (int i = 0; i < m_BlocksMaterials.Length; ++i)
            {
                m_MaterialToBrush.Add(m_BlocksMaterials[i].brushDescriptor.Material,
                                      m_BlocksMaterials[i].brushDescriptor);
            }

            Shader.SetGlobalTexture("_GlobalNoiseTexture", m_GlobalNoiseTexture);

            if (Directory.Exists(App.UserBrushesPath()))
            {
                m_FileWatcher = new FileWatcher(App.UserBrushesPath(), includeSubdirectories: true);
                m_FileWatcher.NotifyFilter = NotifyFilters.LastWrite;
                m_FileWatcher.FileChanged += OnDirectoryChanged;
                m_FileWatcher.FileCreated += OnDirectoryChanged;
                m_FileWatcher.FileDeleted += OnDirectoryChanged;
                m_FileWatcher.EnableRaisingEvents = true;
            }
        }

        private void OnDestroy()
        {
            m_FileWatcher.EnableRaisingEvents = false;
        }

        private static IEnumerable<Brush> KeepOrderDistinct(IEnumerable<Brush> brushes)
        {
            var alreadyFound = new HashSet<string>();
            foreach (var brush in brushes)
            {
                string guid = brush.m_Guid.ToString();
                if (!alreadyFound.Contains(guid))
                {
                    alreadyFound.Add(guid);
                    yield return brush;
                }
            }
        }

        /// Begins reloading any brush assets that come from loose files.
        /// The "BrushCatalogChanged" event will be fired when this is complete.
        public void BeginReload()
        {
            m_CatalogChanged = true;

            // Recreate m_GuidToBrush
            {
                var builtinBrushes = LoadBrushesInManifest();
                builtinBrushes.Add(DefaultBrush);
                m_BuiltinBrushes = KeepOrderDistinct(builtinBrushes).ToDictionary<Brush, Guid>(x => x.m_Guid);
                LoadUserLibraryBrushes();

                // Add reverse links to the brushes
                // Auto-add brushes as compat brushes
                foreach (var brush in builtinBrushes) { brush.m_SupersededBy = null; }
                foreach (var brush in builtinBrushes)
                {
                    var older = brush.m_Supersedes;
                    if (older == null) { continue; }
                    // Add as compat
                    if (!m_BuiltinBrushes.ContainsKey(older.m_Guid))
                    {
                        m_BuiltinBrushes[older.m_Guid] = older;
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
            }

            // Postprocess: put brushes into parse-friendly list
            m_GuiBrushList = AllBrushes.Where(x => !x.m_HiddenInGui).ToList();
        }

        void Update()
        {
            if (m_ChangedBrushes.Count > 0)
            {
                foreach (var path in m_ChangedBrushes)
                {
                    LoadUserLibraryBrush(path);
                }
                m_ChangedBrushes.Clear();
                m_CatalogChanged = true;
            }
            if (m_CatalogChanged)
            {
                m_GuiBrushList = AllBrushes.Where(x => !x.m_HiddenInGui).ToList();
                m_CatalogChanged = false;
                Resources.UnloadUnusedAssets();
                if (BrushCatalogChanged != null)
                {
                    BrushCatalogChanged();
                }
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

        private void LoadUserLibraryBrushes()
        {
            FileUtils.InitializeDirectoryWithUserError(App.UserBrushesPath());
            foreach (var folder in Directory.GetDirectories(App.UserBrushesPath()))
            {
                LoadUserLibraryBrush(folder);
            }
            foreach (var file in Directory.GetFiles(App.UserBrushesPath(), "*.brush"))
            {
                LoadUserLibraryBrush(file);
            }
        }

        private void LoadUserLibraryBrush(string path)
        {
            string brushObject = Path.GetFileName(path);
            BrushDescriptor existingBrush =
              m_LibraryBrushes.Values.FirstOrDefault(x => x.UserVariantBrush.Location == brushObject);
            var userBrush = UserVariantBrush.Create(path);
            if (userBrush == null)
            {
                return;
            }
            if (m_LibraryBrushes.ContainsKey(userBrush.Descriptor.m_Guid) && existingBrush == null)
            {
                Debug.LogError(
                  $"New brush at {path} has a guid already used.");
                return;
            }
            if (userBrush != null)
            {
                m_LibraryBrushes[userBrush.Descriptor.m_Guid] = userBrush.Descriptor;
                if (existingBrush != null)
                {
                    if (BrushController.m_Instance.ActiveBrush.m_Guid == existingBrush.m_Guid)
                    {
                        BrushController.m_Instance.SetBrushToDefault();
                        BrushController.m_Instance.SetActiveBrush(userBrush.Descriptor);
                    }
                }
                m_CatalogChanged = true;
            }
        }

        public void AddSceneBrush(BrushDescriptor brush)
        {
            m_SceneBrushes[brush.m_Guid] = brush;
            m_CatalogChanged = true;
        }

        public void AddMissingSceneBrushFromBase(Guid missingGuid, Guid baseGuid)
        {
            BrushDescriptor baseBrush = GetBrush(baseGuid);
            if (!baseBrush)
            {
                Debug.LogWarning($"Error! Brush {baseBrush} not found!");
                baseBrush = m_DefaultBrush;
            }

            BrushDescriptor missingBrush = Instantiate(baseBrush);
            missingBrush.m_Guid = missingGuid;
            missingBrush.BaseGuid = baseGuid;
            missingBrush.m_Supersedes = null;
            missingBrush.m_SupersededBy = null;
            missingBrush.m_HiddenInGui = true;
            m_SceneBrushes[missingGuid] = missingBrush;
            m_CatalogChanged = true;
        }

        public void ClearSceneBrushes()
        {
            m_SceneBrushes.Clear();
            Resources.UnloadUnusedAssets();
            m_CatalogChanged = true;
        }

        private void OnDirectoryChanged(object source, FileSystemEventArgs e)
        {
            if (e.FullPath.StartsWith(App.UserBrushesPath()))
            {
                int brushPathLength = App.UserBrushesPath().Length + 1;
                var end = e.FullPath.Substring(brushPathLength, e.FullPath.Length - brushPathLength);
                var parts = end.Split(Path.DirectorySeparatorChar);
                string brush = parts.FirstOrDefault();
                if (brush == null)
                {
                    return;
                }
                m_ChangedBrushes.Add(Path.Combine(App.UserBrushesPath(), brush));
            }
        }

    }
}  // namespace TiltBrush
