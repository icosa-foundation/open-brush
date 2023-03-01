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
        static public SketchCatalog m_Instance;

        // This folder contains json files which define where to pull the sketch thumbnail and data
        // from Poly.  These are used to populate the showcase when we can't query Poly.
        // Obviously, if Poly as a database is deleted or moved, accessing these files will fail.
        public const string kDefaultShowcaseSketchesFolder = "DefaultShowcaseSketches";

        private ISketchSet[] m_Sets;

        public ISketchSet GetSet(int i)
        {
            return m_Sets[i];
        }

        public ISketchSet GetSet(string type, string instance)
        {
            return m_Sets.FirstOrDefault(x => x.SketchSetType == type && x.SketchSetInstance == instance);
        }

        public ISketchSet GetFirstSetOrDefault(string type)
        {
            return m_Sets.FirstOrDefault(x => x.SketchSetType == type);
        }

        public ISketchSet GetSetById(string id)
        {
            return m_Sets.FirstOrDefault(x => x.SketchSetId == id);
        }

        public int GetSetIndexById(string id)
        {
            for (int i = 0; i < m_Sets.Length; i++)
            {
                if (m_Sets[i].SketchSetId == id)
                {
                    return i;
                }
            }
            return -1;
        }

        public int GetSetIndex(ISketchSet sketchSet)
        {
            for (int i = 0; i < m_Sets.Length; i++)
            {
                if (m_Sets[i] == sketchSet)
                {
                    return i;
                }
            }
            return -1;
        }

        void Awake()
        {
            m_Instance = this;

            if (Application.platform == RuntimePlatform.OSXEditor ||
                Application.platform == RuntimePlatform.OSXPlayer)
            {
                // force KEvents implementation of FileSystemWatcher
                // source: https://github.com/mono/mono/blob/master/mcs/class/System/System.IO/FileSystemWatcher.cs
                // Unity bug: https://fogbugz.unity3d.com/default.asp?778750_fncnl0np45at4mq1
                System.Environment.SetEnvironmentVariable("MONO_MANAGED_WATCHER", "3");
            }

            int maxTriangles = QualityControls.m_Instance.AppQualityLevels.MaxPolySketchTriangles;

            InitFeaturedSketchesPath();

            //var icosaCollection = new IcosaSketchCollection(App.HttpClient);
            var rssCollection = new RssSketchCollection(App.HttpClient, new Uri("https://timaidley.github.io/open-brush-feed/sketches.rss"));

            m_Sets = new ISketchSet[]
            {
                new FileSketchSet(),
                //new FileSketchSet(App.FeaturedSketchesPath()),
                //new AsyncWrapperSketchSet(new RssSketchSetAsync(new Uri("https://heavenly-upbeat-scorpion.glitch.me/sketches.rss"))),
                //new ResourceCollectionSketchSet(icosaCollection),
                new ResourceCollectionSketchSet(rssCollection),
                new PolySketchSet(this, PolySketchSet.SketchType.Liked, maxTriangles, needsLogin: true),
                new GoogleDriveSketchSet(),
            };
        }

        public static bool InitFeaturedSketchesPath()
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

        void Start()
        {
            foreach (ISketchSet s in m_Sets)
            {
                s.Init();
            }
        }

        void Update()
        {
            foreach (ISketchSet s in m_Sets)
            {
                s.Update();
            }
        }

        public void NotifyUserFileCreated(string fullpath)
        {
            // TODO: This won't work with more tha one filesketchset.
            var userSketches = GetFirstSetOrDefault(FileSketchSet.TypeName);
            userSketches.NotifySketchCreated(fullpath);
        }

        public void NotifyUserFileChanged(string fullpath)
        {
            // TODO: This won't work with more tha one filesketchset.
            var userSketches = GetFirstSetOrDefault(FileSketchSet.TypeName);
            userSketches.NotifySketchChanged(fullpath);
        }
    }


} // namespace TiltBrush
