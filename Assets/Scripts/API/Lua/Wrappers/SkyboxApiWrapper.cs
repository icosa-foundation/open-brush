using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{

    [MoonSharpUserData]
    public class SkyboxApiWrapper
    {
        [MoonSharpHidden]
        public Material _Material;

        public SkyboxApiWrapper(Material material)
        {
            _Material = material;
        }
    }
}
