using MoonSharp.Interpreter;
namespace TiltBrush
{
    [MoonSharpUserData]
    public static class SelectionApiWrapper
    {
        public static void Duplicate() => ApiMethods.Duplicate();
        public static void Group() => ApiMethods.ToggleGroupStrokesAndWidgets();
        public static void Invert() => ApiMethods.InvertSelection();
        public static void Flip() => ApiMethods.FlipSelection();
        public static void Recolor() => ApiMethods.RecolorSelection();
        public static void Rebrush() => ApiMethods.RebrushSelection();
        public static void Resize() => ApiMethods.ResizeSelection();
        public static void Trim(int count) => ApiMethods.TrimSelection(count);
        public static void SelectAll() => ApiMethods.SelectAll();
    }
}
