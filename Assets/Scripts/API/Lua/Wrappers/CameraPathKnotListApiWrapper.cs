﻿using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;

namespace TiltBrush
{
    [LuaDocsDescription("A list of camera path knots")]
    [MoonSharpUserData]
    public class CameraPathKnotListApiWrapper
    {
        [MoonSharpHidden] public List<CameraPathKnot> _Knots;
        [MoonSharpHidden] public CameraPathWidget _CameraPathWidget;

        public CameraPathKnotListApiWrapper(IEnumerable<CameraPathKnot> knots, CameraPathWidget cameraPathWidget)
        {
            _Knots = knots.ToList();
            _CameraPathWidget = cameraPathWidget;
        }

        [LuaDocsDescription("Returns the knot at the given index")]
        public CameraPathKnotApiWrapper this[int index] =>
            new(Utils.WrappedIndexerGet(() => _Knots[index]), _CameraPathWidget);

        [LuaDocsDescription("Returns the last knot")]
        public CameraPathKnotApiWrapper last =>
            _Knots == null || _Knots.Count == 0 ? null : new CameraPathKnotApiWrapper(_Knots[^1], _CameraPathWidget);

        [LuaDocsDescription("The number of knots")]
        public int count => _Knots?.Count ?? 0;
    }
}
