using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
namespace TiltBrush
{
    [MoonSharpUserData]
    public class LayerListApiWrapper
    {
        [MoonSharpHidden]
        public List<CanvasScript> _Layers;
        public LayerApiWrapper last => new LayerApiWrapper(_Layers[^1]);

        public LayerListApiWrapper()
        {
            _Layers = new List<CanvasScript>();
        }

        public LayerListApiWrapper(List<CanvasScript> layers)
        {
            _Layers = layers;
        }
        public LayerApiWrapper this[int index] => new LayerApiWrapper(_Layers[index]);
        public LayerApiWrapper this[string name] => new LayerApiWrapper(_Layers.First(x => x.name == name));
        public  LayerApiWrapper main => new LayerApiWrapper(_Layers[0]);
        public int count => _Layers?.Count ?? 0;

        public LayerApiWrapper active
        {
            get => new LayerApiWrapper(App.Scene.ActiveCanvas);
            set => App.Scene.ActiveCanvas = value._CanvasScript;
        }

    }
}