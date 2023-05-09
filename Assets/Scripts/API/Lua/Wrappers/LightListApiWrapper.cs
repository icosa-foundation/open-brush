using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    [MoonSharpUserData]
    public class LightListApiWrapper
    {
        [MoonSharpHidden]
        public List<Light> _Lights;
        public LightApiWrapper last => new LightApiWrapper(_Lights.Last());

        public LightListApiWrapper()
        {
            _Lights = new List<Light>();
        }

        public LightListApiWrapper(List<Light> lights)
        {
            _Lights = lights;
        }
    }
}