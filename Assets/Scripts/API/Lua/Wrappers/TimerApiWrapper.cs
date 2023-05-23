using MoonSharp.Interpreter;
namespace TiltBrush
{
    [MoonSharpUserData]
    public static class TimerApiWrapper
    {
        public static void Set(Closure fn, float interval, float delay = 0, int repeats = -1) => LuaManager.SetTimer(fn, interval, delay, repeats);
        public static void Unset(Closure fn) => LuaManager.UnsetTimer(fn);
    }
}
