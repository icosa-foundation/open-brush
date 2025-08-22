﻿// Copyright 2020 The Tilt Brush Authors
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

using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TiltBrush
{

    public enum SketchSetType
    {
        User,
        Curated,
        Liked,
        Drive,
        SavedStrokes,
    }

    // SketchCatalog.Awake must come after App.Awake
    public class SketchCatalog : MonoBehaviour
    {
        static public SketchCatalog m_Instance;

        // This folder contains json files which define where to pull the sketch thumbnail and data
        // from Poly.  These are used to populate the showcase when we can't query Poly.
        // Obviously, if Poly as a database is deleted or moved, accessing these files will fail.
        public const string kDefaultShowcaseSketchesFolder = "DefaultShowcaseSketches";

        private SketchSet[] m_Sets;

        public SketchSet GetSet(SketchSetType eType)
        {
            return m_Sets[(int)eType];
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

            SketchSet featuredSketchSet = null;
            if (false) // TODO this fails because of initialization order: (VrAssetService.m_Instance.m_UseLocalFeaturedSketches)
            {
                featuredSketchSet = new FileSketchSet(SketchSetType.Curated);
                InitFeaturedSketchesPath();
            }
            else
            {
                featuredSketchSet = new IcosaSketchSet(this, SketchSetType.Curated);
            }

            m_Sets = new[]
            {
                new FileSketchSet(SketchSetType.User),
                featuredSketchSet,
                new IcosaSketchSet(this, SketchSetType.Liked, needsLogin: true),
                new GoogleDriveSketchSet(),
                new FileSketchSet(SketchSetType.SavedStrokes)
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
            foreach (SketchSet s in m_Sets)
            {
                s.Init();
            }
        }

        void Update()
        {
            foreach (SketchSet s in m_Sets)
            {
                s.Update();
            }
        }

        public void NotifyUserFileCreated(string fullpath)
        {
            if (fullpath.StartsWith(App.SavedStrokesPath()))
            {
                m_Sets[(int)SketchSetType.SavedStrokes].NotifySketchCreated(fullpath);
                // Also notify SavedStrokesCatalog directly for immediate UI updates
                SavedStrokesCatalog.Instance.NotifyFileCreated(fullpath);
            }
            else
            {
                // We only need to notify UserSketchSet
                m_Sets[(int)SketchSetType.User].NotifySketchCreated(fullpath);
            }
        }

        public void NotifyUserFileChanged(string fullpath)
        {
            if (fullpath.StartsWith(App.SavedStrokesPath()))
            {
                m_Sets[(int)SketchSetType.SavedStrokes].NotifySketchCreated(fullpath);
                // Also notify SavedStrokesCatalog directly for immediate UI updates
                SavedStrokesCatalog.Instance.NotifyFileChanged(fullpath);
            }
            else
            {
                // We only need to notify UserSketchSet
                m_Sets[(int)SketchSetType.User].NotifySketchCreated(fullpath);
            }
        }

        private IcosaSketchSet GetIcosaSketchSet(SketchSetType setType)
        {
            var set = GetSet(setType);
            var icosaSketchSet = set as IcosaSketchSet;
            if (icosaSketchSet == null)
            {
                Debug.LogError($"SketchCatalog.QueryOptionParametersForSet: {setType} is not an IcosaSketchSet");
                return null;
            }
            return icosaSketchSet;
        }


        public SketchQueryParameters QueryOptionParametersForSet(SketchSetType setType)
        {
            var icosaSketchSet = GetIcosaSketchSet(setType);
            return icosaSketchSet.m_QueryParams;
        }

        public struct SketchQueryParameters
        {
            public string SearchText;
            public string License;
            public string OrderBy;
            public string Curated;
            public string Category;
        }

        public void UpdateSearchText(SketchSetType setType, string mLastInput, bool forceRefresh = false)
        {
            var queryParams = QueryOptionParametersForSet(setType);
            queryParams.SearchText = mLastInput;
            var icosaAssetSet = GetIcosaSketchSet(setType);
            icosaAssetSet.m_QueryParams = queryParams;
            if (forceRefresh) ForceRefreshPanel();
        }

        public void UpdateLicense(SketchSetType setType, string license, bool forceRefresh = false)
        {
            var queryParams = QueryOptionParametersForSet(setType);
            if (ChoicesHelper.IsValidChoice<LicenseChoices>(license))
            {
                queryParams.License = license;
                var icosaAssetSet = GetIcosaSketchSet(setType);
                icosaAssetSet.m_QueryParams = queryParams;
                if (forceRefresh) ForceRefreshPanel();
            }
        }

        public void UpdateOrderBy(SketchSetType setType, string orderBy, bool forceRefresh = false)
        {
            var queryParams = QueryOptionParametersForSet(setType);
            if (ChoicesHelper.IsValidChoice<OrderByChoices>(orderBy))
            {
                queryParams.OrderBy = orderBy;
                var icosaAssetSet = GetIcosaSketchSet(setType);
                icosaAssetSet.m_QueryParams = queryParams;
                if (forceRefresh) ForceRefreshPanel();
            }
        }

        public void UpdateCurated(SketchSetType setType, string curated, bool forceRefresh = false)
        {
            var queryParams = QueryOptionParametersForSet(setType);
            if (ChoicesHelper.IsValidChoice<CuratedChoices>(curated))
            {
                queryParams.Curated = curated;
                var icosaAssetSet = GetIcosaSketchSet(setType);
                icosaAssetSet.m_QueryParams = queryParams;
                if (forceRefresh) ForceRefreshPanel();
            }
        }

        public void UpdateCategory(SketchSetType setType, string category, bool forceRefresh = false)
        {
            var queryParams = QueryOptionParametersForSet(setType);
            if (ChoicesHelper.IsValidChoice<CategoryChoices>(category))
            {
                queryParams.Category = category;
                var icosaAssetSet = GetIcosaSketchSet(setType);
                icosaAssetSet.m_QueryParams = queryParams;
                if (forceRefresh) ForceRefreshPanel();
            }
        }

        public void RequestForcedRefresh(SketchSetType setType)
        {
            var set = GetIcosaSketchSet(setType);
            set.RequestForcedRefresh();
        }

        private void ForceRefreshPanel()
        {
            var panel = (SketchbookPanel)PanelManager.m_Instance.GetActivePanelByType(BasePanel.PanelType.Sketchbook);
            if (panel == null) panel = (SketchbookPanel)PanelManager.m_Instance.GetActivePanelByType(BasePanel.PanelType.SketchbookMobile);
            if (panel != null)
            {
                panel.ForceRefreshCurrentSet();
            }
        }
    }


} // namespace TiltBrush
