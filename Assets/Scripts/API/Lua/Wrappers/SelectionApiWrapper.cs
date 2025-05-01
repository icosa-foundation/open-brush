using MoonSharp.Interpreter;
namespace TiltBrush
{
    [LuaDocsDescription("Various actions related to selections of strokes and widgets")]
    [MoonSharpUserData]
    public static class SelectionApiWrapper
    {
        public static void Deselect() => SelectionManager.m_Instance.ClearActiveSelection();

        [LuaDocsDescription("Duplicates the currently selected items")]
        [LuaDocsExample("Selection:Duplicate()")]
        public static void Duplicate() => ApiMethods.DuplicateSelection();

        [LuaDocsDescription("Groups or ungroups the currently selected strokes and widgets")]
        [LuaDocsExample("Selection:Group()")]
        public static void Group() => ApiMethods.ToggleGroupStrokesAndWidgets();

        [LuaDocsDescription("Inverts the current selection")]
        [LuaDocsExample("Selection:Invert()")]
        public static void Invert() => ApiMethods.InvertSelection();

        [LuaDocsDescription("Flips (mirrors) the current selected items horizontally")]
        [LuaDocsExample("Selection:Flip()")]
        public static void Flip() => ApiMethods.FlipSelection();

        [LuaDocsDescription("Changes the color of all currently selected brush strokes")]
        [LuaDocsExample("Selection:Recolor()")]
        public static void Recolor() => ApiMethods.RecolorSelection();

        [LuaDocsDescription("Changes the brush type for all currently selected brush strokes")]
        [LuaDocsExample("Selection:Rebrush()")]
        public static void Rebrush() => ApiMethods.RebrushSelection();

        [LuaDocsDescription("Changes the size of all currently selected brush strokes")]
        [LuaDocsExample("Selection:Resize()")]
        public static void Resize() => ApiMethods.ResizeSelection();

        [LuaDocsDescription("Trims control points from all selected brush strokes. Resulting empty strokes are deleted.")]
        [LuaDocsExample("Selection:Trim(5)")]
        [LuaDocsParameter("count", "The number of points to trim from each stroke")]
        public static void Trim(int count) => ApiMethods.TrimSelection(count);
    }
}
