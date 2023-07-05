using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
namespace TiltBrush
{
    [LuaDocsDescription("The list of 3d Models in the scene. (You don't instantiate this yourself. Access this via Sketch.models)")]
    [MoonSharpUserData]
    public class ModelListApiWrapper
    {
        [MoonSharpHidden]
        public List<ModelWidget> _Models;

        [LuaDocsDescription("Returns the last model that was selected")]
        public ModelApiWrapper lastSelected => new ModelApiWrapper(SelectionManager.m_Instance.LastSelectedModel);

        [LuaDocsDescription("Returns the last Model")]
        public ModelApiWrapper last => (_Models == null || _Models.Count == 0) ? null : new ModelApiWrapper(_Models[^1]);

        public ModelListApiWrapper()
        {
            _Models = new List<ModelWidget>();
        }

        public ModelListApiWrapper(List<ModelWidget> models)
        {
            _Models = models;
        }

        [LuaDocsDescription("Returns the model at the specified index")]
        public ModelApiWrapper this[int index] => new ModelApiWrapper(_Models[index]);

        [LuaDocsDescription("The number of models")]
        public int count => _Models?.Count ?? 0;

    }
}