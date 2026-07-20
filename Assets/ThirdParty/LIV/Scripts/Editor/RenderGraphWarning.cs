#if UNITY_6000_0_OR_NEWER
#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LIV.SDK.EditorSupport
{

	[InitializeOnLoad]
	public static class RenderGraphWarning
	{
		static RenderGraphWarning()
		{
			//Somehow it seems that we're still loaded every time we enter play mode
			//EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

			CheckRenderGraph();
		}

		private static void OnPlayModeStateChanged(PlayModeStateChange state)
		{
			if (PlayModeStateChange.EnteredPlayMode == state)
			{
				CheckRenderGraph();
			}
		}

		private static void CheckRenderGraph()
		{
			var settings = GraphicsSettings.GetRenderPipelineSettings<RenderGraphSettings>();
			bool has_render_graph = settings != null;
			bool has_compat_mode = has_render_graph && settings.enableRenderCompatibilityMode;

#if LIV_UNIVERSAL_RENDER
			if (has_render_graph && !has_compat_mode)
				Debug.LogError(
					"When using the LIV SDK with Unity Editor Version >= 6000.0, LIV currently require enabling Compatibility Mode in RenderGraph settings.\nPlease enable Compatibility Mode, see <a href=https://mrc-docs.liv.tv/sdk-for-unity/universal-render-pipeline>the LIV SDK Documentation</a>");
#endif
		}
	}
}

#endif
#endif
