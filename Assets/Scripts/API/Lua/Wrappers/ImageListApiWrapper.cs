using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
namespace TiltBrush
{
    [MoonSharpUserData]
    public class ImageListApiWrapper
    {
        [MoonSharpHidden]
        public List<ImageWidget> _Images;
        public ImageApiWrapper lastSelected => new ImageApiWrapper(SelectionManager.m_Instance.LastSelectedImage);
        public ImageApiWrapper last => (_Images == null || _Images.Count == 0) ? null : new ImageApiWrapper(_Images[^1]);

        public ImageListApiWrapper()
        {
            _Images = new List<ImageWidget>();
        }

        public ImageListApiWrapper(List<ImageWidget> images)
        {
            _Images = images;
        }

        public ImageApiWrapper this[int index] => new ImageApiWrapper(_Images[index]);
        public int count => _Images?.Count ?? 0;
    }
}