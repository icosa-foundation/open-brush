namespace TiltBrush.Layers
{
    public class AddLayerButton : BaseButton
    {
        protected override void OnButtonPressed()
        {
            base.OnButtonPressed();
            App.Scene.AddLayerNow();
        }
    } 
}
