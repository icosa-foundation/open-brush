using UnityEngine;
using UnityEditor;
using TMPro;

public static class CjkAtlasVerify
{
    const string Path = "Assets/Fonts/NotoSansCJK-Light SDF.asset";

    [MenuItem("Tools/CJKFIX Verify Dynamic Glyphs")]
    public static void Run()
    {
        var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Path);
        if (fa == null) { Debug.LogError("CJKVERIFY: font asset not found"); return; }

        // 喬 U+55AC, 髪 U+9AEA, ３ U+FF13 (the exact chars that were boxing out)
        uint[] want = { 0x55AC, 0x9AEA, 0xFF13 };
        bool ok = fa.TryAddCharacters(want, out uint[] missing);

        int missingCount = missing == null ? 0 : missing.Length;
        var t0 = fa.atlasTextures[0];
        Debug.Log($"CJKVERIFY: TryAddCharacters returned={ok} missing={missingCount} " +
                  $"now ch={fa.characterTable.Count} gl={fa.glyphTable.Count} " +
                  $"texPages={fa.atlasTextures.Length} tex0={t0.width}x{t0.height} rw={t0.isReadable}");

        foreach (uint u in want)
            Debug.Log($"CJKVERIFY: U+{u:X4} present={fa.characterLookupTable.ContainsKey(u)}");
    }
}
