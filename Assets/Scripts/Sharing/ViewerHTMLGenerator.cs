using System.IO;
using UnityEngine;

namespace TiltBrush
{
    public static class ViewerHTMLGenerator
    {
        private const string DEFAULT_APP_ID = "tpa958czj7";

        public static string GenerateViewerHTML(string glbPath, string appId = DEFAULT_APP_ID)
        {
            string html = GetHTMLTemplate();
            html = html.Replace("{{APP_ID}}", appId);
            html = html.Replace("{{GLB_PATH}}", glbPath);
            return html;
        }

        private static string GetHTMLTemplate()
        {
            var asset = Resources.Load<TextAsset>("viverse_viewer_template");
            return asset.text;

        }
    }
}
