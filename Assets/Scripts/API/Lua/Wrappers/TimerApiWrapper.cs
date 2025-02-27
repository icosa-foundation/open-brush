using MoonSharp.Interpreter;
namespace TiltBrush
{
    [LuaDocsDescription("Timers can be used to call functions at a predetermined time (or multiple times)")]
    [MoonSharpUserData]
    public static class TimerApiWrapper
    {
        [LuaDocsDescription("Sets up a function to be called in the future")]
        [LuaDocsExample("Timer:Set(myFunction, 1, 0, 5)")]
        [LuaDocsParameter("fn", "The function to call")]
        [LuaDocsParameter("interval", "How long to wait inbetween repeated calls")]
        [LuaDocsParameter("delay", "How long to wait until the first call")]
        [LuaDocsParameter("repeats", @"The number of times to call the function. A value of -1 means ""run forever""")]
        public static void Set(Closure fn, float interval, float delay = 0, int repeats = -1) => LuaManager.SetTimer(fn, interval, delay, repeats);

        [LuaDocsDescription("Removes a future function timer")]
        [LuaDocsExample("Timer:Unset(myFunction)")]
        [LuaDocsParameter("fn", "The function to remove")]
        public static void Unset(Closure fn) => LuaManager.UnsetTimer(fn);
    }
}
