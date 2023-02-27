using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

namespace TiltBrush
{
    public class ResourceSketch : Sketch
    {
        private ResourceFileInfo m_FileInfo;
        public ResourceSketch(ResourceFileInfo fileInfo)
        {
            m_FileInfo = fileInfo;
        }

        public ResourceFileInfo ResourceFileInfo => m_FileInfo;
        public SceneFileInfo SceneFileInfo
        {
            get => m_FileInfo;
            set
            {
                Assert.IsTrue(value is ResourceFileInfo);
            }
        }

        public string[] Authors
        {
            get => m_FileInfo.Resource.Authors.Select(x => x.Name).ToArray();
            set
            {
                throw new NotSupportedException("ResourceSketch does not support setting authors.");
            }
        }
        public Texture2D Icon { get; set; }
        public bool IconAndMetadataValid => Authors != null && Icon != null;
    }
}
