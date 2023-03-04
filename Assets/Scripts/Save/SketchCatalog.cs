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
using System.Reflection;
using System.Text;
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

        private Dictionary<string, ISketchSet> m_Sets = new Dictionary<string, ISketchSet>();
        private Dictionary<string, Func<Dictionary<string, object>, ISketchSet>> m_CollectionCreators;


        private void SetupResourceCollectionCreators()
        {
            m_CollectionCreators = new Dictionary<string, Func<Dictionary<string, object>, ISketchSet>>();

            int maxTriangles = QualityControls.m_Instance.AppQualityLevels.MaxPolySketchTriangles;
            m_CollectionCreators[FileSketchSet.TypeName] = (Dictionary<string, object> _) =>
                new FileSketchSet();
            m_CollectionCreators[PolySketchSet.TypeName] = (Dictionary<string, object> _) =>
                new PolySketchSet(this, PolySketchSet.SketchType.Liked, maxTriangles, needsLogin: true);
            m_CollectionCreators[GoogleDriveSketchSet.TypeName] = (Dictionary<string, object> _) =>
                new GoogleDriveSketchSet();
            m_CollectionCreators["Resource-Rss"] = (Dictionary<string, object> options) =>
                new ResourceCollectionSketchSet(new RssSketchCollection(App.HttpClient, new Uri(options["uri"] as string)));
            m_CollectionCreators["Resource-Path"] = (Dictionary<string, object> options) =>
                new ResourceCollectionSketchSet(new FilesystemSketchCollection(options["path"] as string, (options?["name"] ?? "") as string, options?["icon"] as Texture2D));
            m_CollectionCreators["Resource-Icosa"] = (Dictionary<string, object> options) =>
            {
                object user = null;
                options.TryGetValue("user", out user);
                return new ResourceCollectionSketchSet(new IcosaSketchCollection(App.HttpClient, user as string));
            };
        }

        public static string CreateId(string id, Dictionary<string, object> options)
        {
            var sb = new StringBuilder();
            sb.Append(id);

            if (options != null)
            {
                string add = "?";
                foreach (var pair in options)
                {
                    sb.Append(add);
                    add = "&";
                    sb.Append(pair.Key);
                    sb.Append("=");
                    sb.Append(pair.Value);
                }
            }
            return sb.ToString();
        }

        public static (string type, Dictionary<string, string> options) DecodeId(string id)
        {
            int firstSplit = id.IndexOf("?");
            if (firstSplit < 0)
            {
                return (id, null);
            }
            var type = id.Substring(0, firstSplit);
            var options = new Dictionary<string, string>(
                id.Substring(firstSplit + 1)
                    .Split("&")
                    .Select(x => x.Split("="))
                        .Select(x => new KeyValuePair<string, string>(x[0], x[1])));
            return (type, options);
        }

        public ISketchSet GetSketchSet(string type, Dictionary<string, object> options)
        {
            string id = CreateId(type, options);
            ISketchSet sketchSet = null;
            if (m_Sets.TryGetValue(id, out sketchSet))
            {
                return sketchSet;
            }
            sketchSet = m_CollectionCreators[type].Invoke(options);
            sketchSet.Init();
            m_Sets[id] = sketchSet;
            return sketchSet;
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

            InitFeaturedSketchesPath();

            SetupResourceCollectionCreators();
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

        void Update()
        {
            foreach (ISketchSet s in m_Sets.Values)
            {
                s.Update();
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
