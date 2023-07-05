using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
namespace TiltBrush
{
    [LuaDocsDescription("The list of Images in the scene. (You don't instantiate this yourself. Access this via Sketch.images)")]
    [MoonSharpUserData]
    public class ImageListApiWrapper
    {
        [MoonSharpHidden]
        public List<ImageWidget> _Images;

        [LuaDocsDescription("Returns the last image that was selected")]
        public ImageApiWrapper lastSelected => new ImageApiWrapper(SelectionManager.m_Instance.LastSelectedImage);

        [LuaDocsDescription("Returns the last Image")]
        public ImageApiWrapper last => (_Images == null || _Images.Count == 0) ? null : new ImageApiWrapper(_Images[^1]);

        public ImageListApiWrapper()
        {
            _Images = new List<ImageWidget>();
        }

        public ImageListApiWrapper(List<ImageWidget> images)
        {
            _Images = images;
        }

        [LuaDocsDescription("Returns the image at the specified index")]
        public ImageApiWrapper this[int index] => new ImageApiWrapper(_Images[index]);

        [LuaDocsDescription("The number of images")]
        public int count => _Images?.Count ?? 0;
    }
}