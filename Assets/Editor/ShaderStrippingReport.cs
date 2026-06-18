// Assets/Editor/VariantKeywordTally.cs
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

class VariantKeywordTally : IPreprocessShaders
{
    static Dictionary<string,int> keywordHits = new();

    public int callbackOrder => 0;

    public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
    {
        foreach (var d in data)
        {
            foreach (var kw in d.shaderKeywordSet.GetShaderKeywords())
            {
                var name = ShaderKeyword.GetKeywordName(shader, kw);
                if (string.IsNullOrEmpty(name)) continue;
                keywordHits.TryGetValue(name, out var c);
                keywordHits[name] = c + 1;
            }
        }
    }

    [InitializeOnLoadMethod]
    static void HookBuildEnded()
    {
        BuildPlayerWindow.RegisterBuildPlayerHandler((opts) =>
        {
            keywordHits.Clear();
            BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(opts); // run build
            var path = "VariantKeywordTally.csv";
            File.WriteAllLines(path, keywordHits
                .OrderByDescending(kv => kv.Value)
                .Select(kv => $"{kv.Key},{kv.Value}"));
            Debug.Log($"Variant keyword tally written: {Path.GetFullPath(path)}");
        });
    }
}
