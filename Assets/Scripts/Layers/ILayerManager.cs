using UnityEngine;
namespace TiltBrush.Layers
{
    public interface ILayerManager
    {
        void AddLayer();
        void ClearLayerContents(int mCommandParam);
        void DeleteLayer(int mCommandParam);
        void SetActiveLayer(GameObject parentGameObject);
        void SquashLayer(int mCommandParam);
        void ToggleVisibility(GameObject parentGameObject);
    }
}
