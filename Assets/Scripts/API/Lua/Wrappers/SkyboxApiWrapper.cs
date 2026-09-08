using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    [LuaDocsDescription("The skybox for the current environment")]
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
