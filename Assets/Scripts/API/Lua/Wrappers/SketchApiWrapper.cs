using MoonSharp.Interpreter;
namespace TiltBrush
{
    [MoonSharpUserData]
    public static class SketchApiWrapper
    {
        public static void Open(string name) => ApiMethods.LoadNamedFile(name);
        public static void Save(bool overwrite) => LuaApiMethods.Save(overwrite);
        public static void SaveAs(string name) => LuaApiMethods.SaveAs(name);
        public static void Export() => ApiMethods.ExportRaw();
        public static void NewSketch() => ApiMethods.NewSketch();
        // public static void user() => ApiMethods.LoadUser();
        // public static void curated() => ApiMethods.LoadCurated();
        // public static void liked() => ApiMethods.LoadLiked();
        // public static void drive() => ApiMethods.LoadDrive();
        // public static void exportSelected() => ApiMethods.SaveModel();

        public static StrokeListApiWrapper strokes => new StrokeListApiWrapper(
            SketchMemoryScript.m_Instance.GetAllActiveStrokes()
        );
    }
}
