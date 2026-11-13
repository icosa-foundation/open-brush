using MoonSharp.Interpreter;

namespace TiltBrush
{
    [LuaDocsDescription("Tool related helpers for Tool Scripts")]
    [MoonSharpUserData]
    public static class ToolApiWrapper
    {
        [LuaDocsDescription(
            "Control points for the single path selected by the active Tool Script stroke preview")]
        public static ControlPointListApiWrapper latestControlPoints
        {
            get
            {
                var points = LuaManager.Instance.GetLatestToolScriptControlPoints();
                return new ControlPointListApiWrapper(points);
            }
        }

        [LuaDocsDescription(
            "All paths emitted by the latest Tool Script stroke preview, including symmetry copies. Points are in latestControlPointSpace (canvas space); terminal transforms are omitted. Only paths selected by the preview mode are included; no preview produces an empty list.")]
        public static ControlPointPathListApiWrapper latestControlPointPaths =>
            new(LuaManager.Instance.GetLatestToolScriptControlPointPaths());

        [LuaDocsDescription("The coordinate space for the latest Tool Script control points")]
        public static ScriptCoordSpace latestControlPointSpace => LuaManager.Instance.LatestToolScriptControlPointSpace;
    }
}
