using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
namespace TiltBrush
{
    [LuaDocsDescription("The list of available environments. (Don't create your own instances - use Sketch.environments)")]
    [MoonSharpUserData]
    public class EnvironmentListApiWrapper
    {
        [MoonSharpHidden]
        public List<Environment> _Environments;

        [LuaDocsDescription("Returns the last environment")]
        public EnvironmentApiWrapper last => new EnvironmentApiWrapper(_Environments[^1]);

        [LuaDocsDescription("Returns the current environment")]
        public EnvironmentApiWrapper current
        {
            get
            {
                return new EnvironmentApiWrapper(true);
            }
            set
            {
                SceneSettings.m_Instance.SetDesiredPreset(value._Environment);
            }
        }

        public EnvironmentListApiWrapper()
        {
            _Environments = new List<Environment>();
        }

        public EnvironmentListApiWrapper(List<Environment> environments)
        {
            _Environments = environments;
        }

        [LuaDocsDescription("Returns the environment at the given index")]
        public EnvironmentApiWrapper this[int index] => new EnvironmentApiWrapper(_Environments[index]);

        [LuaDocsDescription("The number of available environments")]
        public int count => _Environments?.Count ?? 0;

        [LuaDocsDescription("Gets an Environment by name")]
        [LuaDocsExample(@"env = Sketch.environments:ByName(""Pistachio"")")]
        [LuaDocsParameter("name", "The name of the environment to get")]
        [LuaDocsReturnValue("The environment, or nil if no environment has that name")]

        public EnvironmentApiWrapper ByName(string name) => new EnvironmentApiWrapper(
            _Environments.FirstOrDefault(e => e.Description == name)
        );
    }

}