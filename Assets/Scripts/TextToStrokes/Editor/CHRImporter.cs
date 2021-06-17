using System.IO;
using UnityEditor.Experimental.AssetImporters;


[ScriptedImporter(0, "chr")]
public class CHRImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext ctx)
    {
        var font = new CHRFont();
        font.DataRaw = File.ReadAllText(ctx.assetPath);
        ctx.AddObjectToAsset("font", font);
        ctx.SetMainObject(font);
    }
}