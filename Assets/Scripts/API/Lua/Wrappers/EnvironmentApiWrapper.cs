using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    [MoonSharpUserData]
    public class EnvironmentApiWrapper
    {
        [MoonSharpHidden]
        public Environment _Environment;
        public EnvironmentApiWrapper(Environment environment)
        {
            _Environment = environment;
        }

        public SkyboxApiWrapper skybox
        {
            get
            {
                return new SkyboxApiWrapper(_Environment.m_SkyboxMaterial);
            }
        }
    }

}
