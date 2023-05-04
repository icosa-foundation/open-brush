using System.Collections.Generic;
using MoonSharp.Interpreter;
namespace TiltBrush
{
    [MoonSharpUserData]
    public static class DrawApiWrapper
    {
        public static void Path(IPathApiWrapper path) => LuaApiMethods.DrawPath(path);
        public static void Paths(IPathApiWrapper paths) => LuaApiMethods.DrawPaths(paths);
        public static void Polygon(int sides, TrTransform tr=default) => DrawStrokes.DrawPolygon(sides, tr);
        public static void Text(string text, TrTransform tr=default) => DrawStrokes.DrawText(text, tr);
        public static void CameraPath(int index) => ApiMethods.DrawCameraPath(index);
    }
}
