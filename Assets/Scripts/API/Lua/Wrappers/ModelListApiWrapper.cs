using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
namespace TiltBrush
{
    [MoonSharpUserData]
    public class ModelListApiWrapper
    {
        [MoonSharpHidden]
        public List<ModelWidget> _Models;
        public ModelApiWrapper lastSelected => new ModelApiWrapper(SelectionManager.m_Instance.LastSelectedModel);
        public ModelApiWrapper last => (_Models == null || _Models.Count == 0) ? null : new ModelApiWrapper(_Models[^1]);

        public ModelListApiWrapper()
        {
            _Models = new List<ModelWidget>();
        }

        public ModelListApiWrapper(List<ModelWidget> models)
        {
            _Models = models;
        }

        public ModelApiWrapper this[int index] => new ModelApiWrapper(_Models[index]);
        public int count => _Models?.Count ?? 0;

    }
}