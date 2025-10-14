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
            var catalog = SketchCatalog.m_Instance.GetSet(SketchSetType.SavedStrokes) as FileSketchSet;
            Thumbnail = catalog.ForceLoadThumbnail(CatalogIndex);
        }
    }
}
