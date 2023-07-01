using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
namespace TiltBrush
{
    [LuaDocsDescription("The list of Guides in the scene. (You don't instantiate this yourself. Access this via Sketch.guides)")]
    [MoonSharpUserData]
    public class GuideListApiWrapper
    {
        [MoonSharpHidden]
        public List<StencilWidget> _Guides;
        public GuideApiWrapper lastSelected => new GuideApiWrapper(SelectionManager.m_Instance.LastSelectedStencil);
        public GuideApiWrapper last => (_Guides == null || _Guides.Count == 0) ? null : new GuideApiWrapper(_Guides[^1]);

        public GuideListApiWrapper()
        {
            _Guides = new List<StencilWidget>();
        }

        public GuideListApiWrapper(List<StencilWidget> guides)
        {
            _Guides = guides;
        }

        public bool enabled
        {
            get => WidgetManager.m_Instance.StencilsDisabled;
            set => WidgetManager.m_Instance.StencilsDisabled = value;
        }

        public GuideApiWrapper this[int index] => new GuideApiWrapper(_Guides[index]);
        public int count => _Guides?.Count ?? 0;

    }
}