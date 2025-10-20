using MoonSharp.Interpreter;

namespace TiltBrush
{
    [LuaDocsDescription("Tool related helpers for Tool Scripts")]
    [MoonSharpUserData]
    public static class ToolApiWrapper
    {
        [LuaDocsDescription("Latest control points produced by the active Tool Script preview")]
        public static ControlPointListApiWrapper latestControlPoints
        {
            get
            {
                var points = LuaManager.Instance.GetLatestToolScriptControlPoints();
                return new ControlPointListApiWrapper(points);
            }
        }

        [LuaDocsDescription("The coordinate space for the latest Tool Script control points")]
        public static ScriptCoordSpace latestControlPointSpace => LuaManager.Instance.LatestToolScriptControlPointSpace;
    }
}
