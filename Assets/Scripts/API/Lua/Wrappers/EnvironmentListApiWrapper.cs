using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
namespace TiltBrush
{
    [MoonSharpUserData]
    public class EnvironmentListApiWrapper
    {
        [MoonSharpHidden]
        public List<Environment> _Environments;
        public EnvironmentApiWrapper last => new EnvironmentApiWrapper(_Environments.Last());

        public EnvironmentListApiWrapper()
        {
            _Environments = new List<Environment>();
        }

        public EnvironmentListApiWrapper(List<Environment> environments)
        {
            _Environments = environments;
        }
    }

}