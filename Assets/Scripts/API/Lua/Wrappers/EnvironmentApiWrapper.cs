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

        public override string ToString()
        {
            return $"Environment({_Environment.m_Description})";
        }

    }

}
