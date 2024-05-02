// Copyright 2023 The Tilt Brush Authors
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

namespace TiltBrush
{

    public class BackgroundImageCatalog : ReferenceImageCatalog
    {
        static public BackgroundImageCatalog m_Instance;
        protected string m_CurrentBackgroundImagesDirectory;
        public string CurrentBackgroundImagesDirectory => m_CurrentBackgroundImagesDirectory;

        void Awake()
        {
            m_Instance = this;
            m_RequestedLoads = new Stack<int>();

            App.InitMediaLibraryPath();
            App.InitBackgroundImagesPath(m_DefaultImages);
            ChangeDirectory(HomeDirectory);
        }

        public override void ChangeDirectory(string newPath)
        {
            m_CurrentBackgroundImagesDirectory = newPath;
            if (Directory.Exists(m_CurrentBackgroundImagesDirectory))
            {
                m_FileWatcher = new FileWatcher(m_CurrentBackgroundImagesDirectory);
                m_FileWatcher.NotifyFilter = NotifyFilters.LastWrite;
                m_FileWatcher.FileChanged += OnChanged;
                m_FileWatcher.FileCreated += OnChanged;
                m_FileWatcher.FileDeleted += OnChanged;
                m_FileWatcher.EnableRaisingEvents = true;
            }
            m_Images = new List<ReferenceImage>();
            ProcessReferenceDirectory(userOverlay: false);
        }

        public override string HomeDirectory => App.BackgroundImagesLibraryPath();
        public override bool IsHomeDirectory() => m_CurrentBackgroundImagesDirectory == HomeDirectory;

        public override bool IsSubDirectoryOfHome()
        {
            return m_CurrentBackgroundImagesDirectory.StartsWith(HomeDirectory);
        }

        protected override bool ValidExtension(string ext)
        {
            return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".hdr";
        }

        protected override void ProcessReferenceDirectory(bool userOverlay = true)
        {
            _ProcessReferenceDirectory_Impl(m_CurrentBackgroundImagesDirectory, userOverlay);
        }

        public override string GetCurrentDirectory()
        {
            return m_CurrentBackgroundImagesDirectory;
        }

    }
} // namespace TiltBrush
