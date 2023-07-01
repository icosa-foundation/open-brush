using System.Linq;
using MoonSharp.Interpreter;

namespace TiltBrush
{
    [LuaDocsDescription("Functions related to SVG images")]
    [MoonSharpUserData]
    public static class SvgApiWrapper
    {
        public static MultiPathApiWrapper ParsePathString(string svgPath) => _SvgPathStringToNestedPaths(svgPath);
        public static MultiPathApiWrapper ParseDocument(string svg, float offsetPerPath = 0, bool includeColors = false)
        {
            return _SvgDocumentToNestedPaths(svg, offsetPerPath, includeColors);
        }

        public static void DrawPathString(string svg, TrTransform tr=default) => DrawStrokes.DrawSvgPathString(svg, tr);
        public static void DrawDocument(string svg, TrTransform tr=default) => DrawStrokes.DrawSvg(svg, tr);

        private static MultiPathApiWrapper _SvgDocumentToNestedPaths(string svg, float offsetPerPath, bool includeColors)
        {
            var (nestedTransforms, colors) = DrawStrokes.SvgDocumentToNestedPaths(svg, offsetPerPath, includeColors);
            var paths = nestedTransforms.Select(p => new PathApiWrapper(p));
            return new MultiPathApiWrapper(paths, colors);
        }

        private static MultiPathApiWrapper _SvgPathStringToNestedPaths(string svgPathString)
        {
            var nestedTransforms = DrawStrokes.SvgPathStringToApiPaths(svgPathString);
            var paths = nestedTransforms.Select(p => new PathApiWrapper(p));
            return new MultiPathApiWrapper(paths);
        }

    }
}
