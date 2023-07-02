using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;

namespace TiltBrush
{
    [LuaDocsDescription("The list of Videos in the scene. (You don't instantiate this yourself. Access this via Sketch.videos)")]
    [MoonSharpUserData]
    public class VideoListApiWrapper
    {
        [MoonSharpHidden]
        public List<VideoWidget> _Videos;

        [LuaDocsDescription("Returns the last Video that was selected")]
        public VideoApiWrapper lastSelected => new VideoApiWrapper(SelectionManager.m_Instance.LastSelectedVideo);

        [LuaDocsDescription("Returns the last Video")]
        public VideoApiWrapper last => (_Videos == null || _Videos.Count == 0) ? null : new VideoApiWrapper(_Videos[^1]);

        public VideoListApiWrapper()
        {
            _Videos = new List<VideoWidget>();
        }

        public VideoListApiWrapper(List<VideoWidget> videos)
        {
            _Videos = videos;
        }

        [LuaDocsDescription("Returns the video at the specified index")]
        public VideoApiWrapper this[int index] => new VideoApiWrapper(_Videos[index]);

        [LuaDocsDescription("The number of videos")]
        public int count => _Videos?.Count ?? 0;
    }
}