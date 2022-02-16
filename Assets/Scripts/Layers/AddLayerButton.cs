using System.Linq;
namespace TiltBrush.Layers
{
    public class AddLayerButton : BaseButton
    {
        protected override void OnButtonPressed()
        {
            var UiManager = GetComponentInParent<LayerUI_Manager>();
            if (App.Scene.LayerCanvases.Count() >= UiManager.m_Widgets.Count) return;
            base.OnButtonPressed();
            UiManager.AddLayer();
        }
    } 
}
