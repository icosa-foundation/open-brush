using UnityEngine;
using System.Collections;

namespace uGIF
{
	public class Image
	{
		public int width;
		public int height;
		public Color32[] pixels;

		public Image(Texture2D f)
		{
			pixels = f.GetPixels32();
			width = f.width;
			height = f.height;
		}

		public Image(Image image)
		{
			pixels = image.pixels.Clone() as Color32[];
			width = image.width;
			height = image.height;
		}

		public Image(int width, int height)
		{
			this.width = width;
			this.height = height;
			pixels = new Color32[width * height];
		}

		public Image(int width, int height, Color32[] pixels)
		{
			this.width = width;
			this.height = height;
			this.pixels = pixels;
		}

		public void DrawImage(Image image, int i, int i2)
		{
			throw new System.NotImplementedException();
		}

		public Color32 GetPixel(int tw, int th)
		{
			var index = (th * width) + tw;
			return pixels[index];
		}

		public void Flip()
		{
			for (var y = 0; y < height / 2; y++)
			{
				for (var x = 0; x < width; x++)
				{
					var top = y * width + x;
					var bottom = (height - y - 1) * width + x;
					var temp = pixels[top];
					pixels[top] = pixels[bottom];
					pixels[bottom] = temp;
				}
			}

		}

		public void Resize(int scale)
		{
			if (scale <= 1)
				return;
			var newWidth = width / scale;
			var newHeight = height / scale;
			var newColors = new Color32[newWidth * newHeight];
			for (var y = 0; y < newHeight; y++)
			{
				for (var x = 0; x < newWidth; x++)
				{
					newColors[(y * newWidth) + x] = pixels[(y * scale) * width + (x * scale)];
				}
			}
			pixels = newColors;
			height = newHeight;
			width = newWidth;
		}

		public void ResizeBilinear(int newWidth, int newHeight)
		{
			if (newWidth == width && newHeight == height)
				return;
			var texColors = pixels;
			var newColors = new Color32[newWidth * newHeight];
			var ratioX = 1.0f / ((float)newWidth / (width - 1));
			var ratioY = 1.0f / ((float)newHeight / (height - 1));
			var w = width;
			var w2 = newWidth;

			for (var y = 0; y < newHeight; y++)
			{
				var yFloor = Mathf.FloorToInt(y * ratioY);
				var y1 = yFloor * w;
				var y2 = (yFloor + 1) * w;
				var yw = y * w2;

				for (var x = 0; x < w2; x++)
				{
					int xFloor = (int)Mathf.Floor(x * ratioX);
					var xLerp = x * ratioX - xFloor;
					newColors[yw + x] = ColorLerpUnclamped(ColorLerpUnclamped(texColors[y1 + xFloor], texColors[y1 + xFloor + 1], xLerp), ColorLerpUnclamped(texColors[y2 + xFloor], texColors[y2 + xFloor + 1], xLerp), y * ratioY - yFloor);
				}
			}
			pixels = newColors;
			height = newHeight;
			width = newWidth;

		}

		Color32 ColorLerpUnclamped(Color A, Color B, float P)
		{

			return new Color(A.r + (B.r - A.r) * P, A.g + (B.g - A.g) * P, A.b + (B.b - A.b) * P, A.a + (B.a - A.a) * P);
		}
	}
}