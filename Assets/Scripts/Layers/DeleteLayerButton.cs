namespace TiltBrush.Layers
{
    public class DeleteLayerButton : OptionButton
    {
        protected override void OnButtonPressed() 
        { 
            base.OnButtonPressed();
            GetComponentInParent<LayerUI_Manager>().DeleteLayer(transform.parent.gameObject);
        }
        
    }
}
