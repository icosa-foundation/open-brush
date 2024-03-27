using System.Linq;
using MoonSharp.Interpreter;

namespace TiltBrush
{
    [LuaDocsDescription("Functions related to SVG images")]
    [MoonSharpUserData]
    public static class SvgApiWrapper
    {
        [LuaDocsDescription("Parses an SVG path string")]
        [LuaDocsExample("myPaths = SVG:ParsePathString('M 100 100 L 200 200')")]
        [LuaDocsParameter("svgPath", "The SVG path string to parse")]
        [LuaDocsReturnValue("Returns a PathList representing the parsed SVG path")]
        public static PathListApiWrapper ParsePathString(string svgPath) => _SvgPathStringToNestedPaths(svgPath);

        [LuaDocsDescription("Parses an SVG document")]
        [LuaDocsExample("myPaths = SVG:ParseDocument('<svg>...</svg>')")]
        [LuaDocsParameter("svg", "A text string that is valid SVG document")]
        [LuaDocsParameter("offsetPerPath", "Each path can be lifted to form a layered result")]
        [LuaDocsParameter("includeColors", "Whether the colors from the SVG are used")]
        [LuaDocsReturnValue("Returns a PathList representing the parsed SVG document")]
        public static PathListApiWrapper ParseDocument(string svg, float offsetPerPath = 0, bool includeColors = false)
        {
            return _SvgDocumentToNestedPaths(svg, offsetPerPath, includeColors);
        }

        private static PathListApiWrapper _SvgDocumentToNestedPaths(string svg, float offsetPerPath, bool includeColors)
        {
            var (nestedTransforms, colors) = DrawStrokes.SvgDocumentToNestedPaths(svg, offsetPerPath, includeColors);
            var paths = nestedTransforms.Select(p => new PathApiWrapper(p));
            return new PathListApiWrapper(paths, colors);
        }

        private static PathListApiWrapper _SvgPathStringToNestedPaths(string svgPathString)
        {
            var nestedTransforms = DrawStrokes.SvgPathStringToApiPaths(svgPathString);
            var paths = nestedTransforms.Select(p => new PathApiWrapper(p));
            return new PathListApiWrapper(paths);
        }

        [LuaDocsDescription("Draws an SVG path string")]
        [LuaDocsExample("Svg:DrawPathString('M 100 100 L 200 200')")]
        [LuaDocsParameter("svg", "The SVG path string to draw")]
        [LuaDocsParameter("tr", "The transform to apply to the result")]
        public static void DrawPathString(string svg, TransformApiWrapper tr = null) =>
            DrawStrokes.DrawSvgPathString(svg, (tr ?? TransformApiWrapper.identity)._TrTransform);

        [LuaDocsDescription("Draws an SVG document")]
        [LuaDocsExample("Svg:Draw('<svg>...</svg>')")]
        [LuaDocsParameter("svg", "A text string that is a valid SVG document")]
        [LuaDocsParameter("tr", "The transform (position, rotation and scale) to apply to the result")]
        public static void DrawDocument(string svg, TrTransform tr = default) =>
            DrawStrokes.DrawSvg(svg, tr);
    }
}
