using UnityEngine;

[ExecuteInEditMode]
public class TiltShift : MonoBehaviour
{
	public Material mat;

	// Separate from enabled: URP keeps the legacy OnRenderImage component disabled.
	public bool SuppressForCapture { get; set; }

	private void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		if (mat == null)
			mat = new Material(Shader.Find("FX/TiltShift"));

		Graphics.Blit(source, destination, mat);
	}
}
