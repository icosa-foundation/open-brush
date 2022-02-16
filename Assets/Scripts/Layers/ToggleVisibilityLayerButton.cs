namespace TiltBrush.Layers
{
    public class ToggleVisibilityLayerButton : ToggleButton
    {
        protected override void OnButtonPressed()
        {
            base.OnButtonPressed();
            GetComponentInParent<LayerUI_Manager>().ToggleVisibility(transform.parent.gameObject);
        }
    }
}
