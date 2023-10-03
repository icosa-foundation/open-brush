using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
namespace TiltBrush
{
    [LuaDocsDescription("The list of Layers in the scene. (You don't instantiate this yourself. Access this via Sketch.layers)")]
    [MoonSharpUserData]
    public class LayerListApiWrapper
    {
        [MoonSharpHidden]
        public List<CanvasScript> _Layers;

        [LuaDocsDescription("Returns the last layer")]
        public LayerApiWrapper last => new LayerApiWrapper(_Layers[^1]);

        public LayerListApiWrapper()
        {
            _Layers = new List<CanvasScript>();
        }

        public LayerListApiWrapper(List<CanvasScript> layers)
        {
            _Layers = layers;
        }

        [LuaDocsDescription("Returns the layer at the given index")]
        public LayerApiWrapper this[int index] => new LayerApiWrapper(_Layers[index]);

        [LuaDocsDescription("Returns the layer with the given name")]
        public LayerApiWrapper this[string name] => new LayerApiWrapper(_Layers.First(x => x.name == name));

        [LuaDocsDescription("Returns the main layer")]
        public LayerApiWrapper main => new LayerApiWrapper(_Layers[0]);

        [LuaDocsDescription("The number of layers")]
        public int count => _Layers?.Count ?? 0;

        [LuaDocsDescription("Returns the active layer")]
        public LayerApiWrapper active
        {
            get => new LayerApiWrapper(App.Scene.ActiveCanvas);
            set => App.Scene.ActiveCanvas = value._CanvasScript;
        }

    }
}