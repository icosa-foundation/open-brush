using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
namespace TiltBrush
{
    public class RemoteSketch : Sketch
    {
        public SceneFileInfo SceneFileInfo { get; set; }
        public string[] Authors { get; set; }
        public Texture2D Icon { get; set; }
        public bool IconAndMetadataValid => Authors != null && Icon != null;
    }

    public interface ISketchSetAsync
    {
        public string Name { get; }

        public Task InitAsync();

        public Task<RemoteSketch[]> FetchSketchPageAsync(int page);
    }
}
