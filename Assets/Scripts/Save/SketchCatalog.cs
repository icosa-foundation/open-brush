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

namespace TiltBrush
{
    // SketchCatalog.Awake must come after App.Awake
    public class SketchCatalog : MonoBehaviour
    {
        public static SketchCatalog m_Instance;

        // This folder contains json files which define where to pull the sketch thumbnail and data
        // from Poly.  These are used to populate the showcase when we can't query Poly.
        // Obviously, if Poly as a database is deleted or moved, accessing these files will fail.
        public const string kDefaultShowcaseSketchesFolder = "DefaultShowcaseSketches";

        private PolySketchSet m_polySketchSet;
        private GoogleDriveSketchSet m_googleDriveSketchSet = new();

        private Dictionary<Uri, WeakReference<ISketchSet>> m_Sets = new();

        public ISketchSet GetSketchSet(string uri)
        {
            return GetSketchSet(new Uri(uri));
        }

        public ISketchSet GetSketchSet(Uri uri)
        {
            ISketchSet sketchSet = null;
            if (m_Sets.TryGetValue(uri, out WeakReference<ISketchSet> sketchSetRef))
            {
                if (sketchSetRef.TryGetTarget(out sketchSet))
                {
                    return sketchSet;
                }
                m_Sets.Remove(uri);
            }

            sketchSet = new ResourceCollectionSketchSet(ResourceCollectionFactory.Instance.FetchCollection(uri));
            sketchSet.Init();
            if (sketchSet != null)
            {
                m_Sets[uri] = new WeakReference<ISketchSet>(sketchSet);
            }
            return sketchSet;
        }


        void Awake()
        {
            m_Instance = this;

            m_polySketchSet = new PolySketchSet(this, PolySketchSet.SketchType.Curated, 100000);
            m_Sets[new Uri("poly:")] = new WeakReference<ISketchSet>(m_polySketchSet);
            m_Sets[new Uri("googledrive:")] = new WeakReference<ISketchSet>(m_googleDriveSketchSet);

            if (Application.platform == RuntimePlatform.OSXEditor ||
                Application.platform == RuntimePlatform.OSXPlayer)
            {
                // force KEvents implementation of FileSystemWatcher
                // source: https://github.com/mono/mono/blob/master/mcs/class/System/System.IO/FileSystemWatcher.cs
                // Unity bug: https://fogbugz.unity3d.com/default.asp?778750_fncnl0np45at4mq1
                System.Environment.SetEnvironmentVariable("MONO_MANAGED_WATCHER", "3");
            }

            InitFeaturedSketchesPath();
        }

        void Start()
        {
            m_polySketchSet.Init();
            m_googleDriveSketchSet.Init();
        }

        private static bool InitFeaturedSketchesPath()
        {
            string featuredPath = App.FeaturedSketchesPath();
            if (!App.InitDirectoryAtPath(featuredPath)) { return false; }

            TextAsset[] textAssets =
                Resources.LoadAll<TextAsset>(SketchCatalog.kDefaultShowcaseSketchesFolder);
            foreach (var asset in textAssets)
            {
                if (asset.name.EndsWith(".tilt"))
                {
                    string filePath = Path.Combine(App.FeaturedSketchesPath(), asset.name);
                    if (!File.Exists(filePath))
                    {
                        File.WriteAllBytes(filePath, asset.bytes);
                    }
                }
                Resources.UnloadAsset(asset);
            }

            return true;
        }

        void Update()
        {
            foreach (var entry in m_Sets.ToArray())
            {
                ISketchSet sketchSet;
                if (entry.Value.TryGetTarget(out sketchSet))
                {
                    sketchSet.Update();
                }
                else
                {
                    m_Sets.Remove(entry.Key);
                }
            }
        }

        public void NotifyUserFileCreated(string fullpath)
        {
            // TODO: This won't work with more tha one filesketchset.
            var userSketches = SketchbookPanel.Instance.GetSketchSet(SketchbookPanel.RootSet.Local);
            userSketches.NotifySketchCreated(fullpath);
        }

        public void NotifyUserFileChanged(string fullpath)
        {
            // TODO: This won't work with more tha one filesketchset.
            var userSketches = SketchbookPanel.Instance.GetSketchSet(SketchbookPanel.RootSet.Local);
            userSketches.NotifySketchChanged(fullpath);
        }
    }


} // namespace TiltBrush
