namespace TiltBrush.Layers
{
    public class ClearLayerContentsButton : BaseButton
    {
        protected override void OnButtonPressed()
        {
            base.OnButtonPressed();
            GetComponentInParent<LayerUI_Manager>().ClearLayerContents(transform.parent.gameObject);
        }
    }
}
