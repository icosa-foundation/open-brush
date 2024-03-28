using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

namespace TiltBrush
{
    public class ResourceSketch : ISketch
    {
        private ResourceFileInfo m_FileInfo;
        private static readonly string[] emptyStringArray = new string[] { };
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
                m_FileInfo = (ResourceFileInfo)value;
            }
        }

        public string[] Authors
        {
            get => m_FileInfo.Resource.Authors?.Select(x => x.Name).ToArray() ?? emptyStringArray;
            set
            {
                throw new NotSupportedException("ResourceSketch does not support setting authors.");
            }
        }
        public Texture2D Icon { get; set; }
        public bool IconAndMetadataValid => Authors != null && Icon != null;
    }
}
