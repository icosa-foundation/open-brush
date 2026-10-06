using UnityEngine;

namespace TiltBrush
{
    public class SavedStrokeFile
    {
        public int CatalogIndex { get; private set; }
        public SceneFileInfo FileInfo { get; private set; }
        public Texture2D Thumbnail { get; private set; }

        public SavedStrokeFile(int i, SceneFileInfo sceneFileInfo, Texture2D thumbnail)
        {
            CatalogIndex = i;
            FileInfo = sceneFileInfo;
            Thumbnail = thumbnail;
        }


        public void ForceLoadThumbnail()
        {
            var catalog = SketchCatalog.m_Instance.GetSet(SketchSetType.SavedStrokes);
            if (catalog is FileSketchSet fileSketchSet)
            {
                Thumbnail = fileSketchSet.ForceLoadThumbnail(CatalogIndex);
                return;
            }

            byte[] data = FileSketchSet.ReadThumbnail(FileInfo);
            if (data == null || data.Length == 0)
            {
                return;
            }
            var thumbnail = new Texture2D(128, 128, TextureFormat.RGB24, true);
            thumbnail.LoadImage(data);
            thumbnail.Apply();
            Thumbnail = thumbnail;
        }
    }
}
