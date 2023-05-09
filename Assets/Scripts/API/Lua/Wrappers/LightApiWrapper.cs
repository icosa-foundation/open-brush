using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    [MoonSharpUserData]
    public class LightApiWrapper
    {
        [MoonSharpHidden]
        public Light _Light;
        public LightApiWrapper(Light light)
        {
            _Light = light;
        }
    }
}
