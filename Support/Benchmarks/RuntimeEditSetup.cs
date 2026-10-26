// Configure a fixed render resolution before RuntimeEditWorkload; restore the Editor layout choice afterward.
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class RuntimeEditSetup
{
    const string Key = "RuntimeEditViewport";
    const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    static object Group(out EditorWindow window, out PropertyInfo selected)
    {
        var assembly = typeof(Editor).Assembly;
        var viewType = assembly.GetType("UnityEditor.GameView");
        window = EditorWindow.GetWindow(viewType);
        selected = viewType.GetProperty("selectedSizeIndex", Flags);
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        var groupType = sizesType.GetProperty("currentGroupType", Flags).GetValue(sizes);
        return sizesType.GetMethod("GetGroup", Flags).Invoke(sizes, new[] { groupType });
    }

    public static string Configure()
    {
        if (SessionState.GetBool(Key, false)) return "[RuntimeEditBenchmark] Fixed viewport already configured";
        var group = Group(out var window, out var selected);
        SessionState.SetInt($"{Key}.original", (int)selected.GetValue(window));
        var type = group.GetType();
        int count = (int)type.GetMethod("GetTotalCount", Flags).Invoke(group, null);
        int target = -1;
        for (int i = 0; i < count; i++)
        {
            var size = type.GetMethod("GetGameViewSize", Flags).Invoke(group, new object[] { i });
            var sizeType = size.GetType();
            if ((int)sizeType.GetProperty("width", Flags).GetValue(size) == 1280 &&
                (int)sizeType.GetProperty("height", Flags).GetValue(size) == 720) { target = i; break; }
        }
        if (target < 0)
        {
            var assembly = typeof(Editor).Assembly;
            var sizeType = assembly.GetType("UnityEditor.GameViewSize");
            var kind = Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"), "FixedResolution");
            var size = Activator.CreateInstance(sizeType, Flags, null,
                new object[] { kind, 1280, 720, "OB_RuntimeBenchmark_1280x720" }, null);
            type.GetMethod("AddCustomSize", Flags).Invoke(group, new[] { size });
            target = count;
            SessionState.SetInt($"{Key}.added", count - (int)type.GetMethod("GetBuiltinCount", Flags).Invoke(group, null));
        }
        else SessionState.SetInt($"{Key}.added", -1);
        selected.SetValue(window, target); window.Repaint();
        SessionState.SetBool(Key, true);
        return "[RuntimeEditBenchmark] Configured 1280 x 720; workload verifies actual Screen dimensions";
    }

    public static string Restore()
    {
        if (!SessionState.GetBool(Key, false)) return "[RuntimeEditBenchmark] No viewport change to restore";
        var group = Group(out var window, out var selected);
        selected.SetValue(window, SessionState.GetInt($"{Key}.original", 0));
        int added = SessionState.GetInt($"{Key}.added", -1);
        if (added >= 0) group.GetType().GetMethod("RemoveCustomSize", Flags).Invoke(group, new object[] { added });
        SessionState.SetBool(Key, false); window.Repaint();
        return "[RuntimeEditBenchmark] Restored original Game view size";
    }
}
