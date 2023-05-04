using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    [MoonSharpUserData]
    public static class SvgApiWrapper
    {
        public static MultiPathApiWrapper ParsePathString(string svgPath) => _SvgPathStringToNestedPaths(svgPath);
        public static MultiPathApiWrapper ParseDocument(string svg) => _SvgDocumentToNestedPaths(svg);

        public static void DrawPathString(string svg, TrTransform tr=default) => DrawStrokes.DrawSvgPathString(svg, tr);
        public static void DrawDocument(string svg, TrTransform tr=default) => DrawStrokes.DrawSvg(svg, tr);

        private static MultiPathApiWrapper _SvgDocumentToNestedPaths(string svg)
        {
            var nestedTransforms = DrawStrokes.SvgDocumentToNestedPaths(svg);
            var paths = nestedTransforms.Select(p => new PathApiWrapper(p));
            return new MultiPathApiWrapper(paths);
        }

        private static MultiPathApiWrapper _SvgPathStringToNestedPaths(string svgPathString)
        {
            var nestedTransforms = DrawStrokes.SvgPathStringToApiPaths(svgPathString);
            var paths = nestedTransforms.Select(p => new PathApiWrapper(p));
            return new MultiPathApiWrapper(paths);
        }

    }
}
