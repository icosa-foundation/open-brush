using UnityEngine;

namespace TiltBrush
{
    public class SavedStrokeFile
    {
        public SavedStrokeFile(SceneFileInfo sceneFileInfo, Texture2D thumbnail)
        {
            FileInfo = sceneFileInfo;
            Thumbnail = thumbnail;
        }

        public SceneFileInfo FileInfo { get; private set; }
        public Texture2D Thumbnail { get; private set; }
    }
}
