using MoonSharp.Interpreter;
namespace TiltBrush
{
    [LuaDocsDescription("Various actions related to selections of strokes and widgets")]
    [MoonSharpUserData]
    public static class SelectionApiWrapper
    {
        public static void Duplicate() => ApiMethods.DuplicateSelection();
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
