using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
namespace TiltBrush
{
    [MoonSharpUserData]
    public class VideoListApiWrapper
    {
        [MoonSharpHidden]
        public List<VideoWidget> _Videos;
        public VideoApiWrapper lastSelected => new VideoApiWrapper(SelectionManager.m_Instance.LastSelectedVideo);
        public VideoApiWrapper last => new VideoApiWrapper(_Videos[count - 1]);

        public VideoListApiWrapper()
        {
            _Videos = new List<VideoWidget>();
        }

        public VideoListApiWrapper(List<VideoWidget> videos)
        {
            _Videos = videos;
        }

        public VideoApiWrapper this[int index] => new VideoApiWrapper(_Videos[index]);
        public int count => _Videos.Count;


    }
}