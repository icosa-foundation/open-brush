using System;
using System.IO;
using UnityEngine;
using System.Collections.Generic;

namespace uGIF
{
	public class GIFEncoder
	{

		public bool useGlobalColorTable = false;
		public Color32? transparent = null;
		public int repeat = -1;
		public int dispose = -1; // disposal code (-1 = use default)
		public int quality = 10; // default sample interval for quantizer

		public float FPS
		{
			set
			{
				delay = Mathf.RoundToInt(100f / value);
			}
		}

		public void AddFrame(Image im)
		{
			if (im == null)
				throw new ArgumentNullException("im");
			if (!started)
				throw new InvalidOperationException("Start() must be called before AddFrame()");

			if (firstFrame)
			{
				width = im.width;
				height = im.height;
			}

			pixels = im.pixels;
			RemapPixels(); // build color table & map pixels
			pixels = null;
			if (firstFrame)
			{
				WriteLSD(); // logical screen descriptior
				WritePalette(); // global color table
				if (repeat >= 0)
				{
					// use NS app extension to indicate reps
					WriteNetscapeExt();
				}
			}

			WriteGraphicCtrlExt(); // write graphic control extension
			WriteImageDesc(); // image descriptor
			if (!firstFrame && !useGlobalColorTable)
			{
				WritePalette(); // local color table
			}
			WritePixels(); // encode and write pixel data
			firstFrame = false;
		}

		public void Finish()
		{
			if (!started)
				throw new InvalidOperationException("Start() must be called before Finish()");

			started = false;

			ms.WriteByte(0x3b); // gif trailer
			ms.Flush();

			// reset for subsequent use
			transIndex = 0;
			pixels = null;
			indexedPixels = null;
			prevIndexedPixels = null;
			colorTab = null;
			firstFrame = true;
			nq = null;
		}

		public void Start(MemoryStream os)
		{
			if (os == null)
				throw new ArgumentNullException("os");
			ms = os;
			started = true;
			WriteString("GIF89a"); // header
		}

		void RemapPixels()
		{
			int len = pixels.Length;
			indexedPixels = new byte[len];

			if (firstFrame || !useGlobalColorTable)
			{
				// initialize quantizer
				nq = new NeuQuant(pixels, len, quality);
				colorTab = nq.Process(); // create reduced palette
			}

			for (int i = 0; i < len; i++)
			{
				int index = nq.Map(pixels[i].r & 0xff, pixels[i].g & 0xff, pixels[i].b & 0xff);
				usedEntry[index] = true;
				indexedPixels[i] = (byte)index;

				if (dispose == 1 && prevIndexedPixels != null)
				{
					if (indexedPixels[i] == prevIndexedPixels[i])
					{
						indexedPixels[i] = (byte)transIndex;
					}
					else
					{
						prevIndexedPixels[i] = (byte)index;
					}
				}
			}
			colorDepth = 8;
			palSize = 7;
			// get closest match to transparent color if specified
			if (transparent.HasValue)
			{
				var c = transparent.Value;
				//transIndex = FindClosest(transparent);
				transIndex = nq.Map(c.b, c.g, c.r);
			}

			if (dispose == 1 && prevIndexedPixels == null)
				prevIndexedPixels = indexedPixels.Clone() as byte[];
		}

		int FindClosest(Color32 c)
		{
			if (colorTab == null)
				return -1;
			int r = c.r;
			int g = c.g;
			int b = c.b;
			int minpos = 0;
			int dmin = 256 * 256 * 256;
			int len = colorTab.Length;
			for (int i = 0; i < len;)
			{
				int dr = r - (colorTab[i++] & 0xff);
				int dg = g - (colorTab[i++] & 0xff);
				int db = b - (colorTab[i] & 0xff);
				int d = dr * dr + dg * dg + db * db;
				int index = i / 3;
				if (usedEntry[index] && (d < dmin))
				{
					dmin = d;
					minpos = index;
				}
				i++;
			}
			return minpos;
		}

		void WriteGraphicCtrlExt()
		{
			ms.WriteByte(0x21); // extension introducer
			ms.WriteByte(0xf9); // GCE label
			ms.WriteByte(4); // data block size
			int transp, disp;
			if (transparent == null)
			{
				transp = 0;
				disp = 0; // dispose = no action
			}
			else
			{
				transp = 1;
				disp = 2; // force clear if using transparent color
			}
			if (dispose >= 0)
			{
				disp = dispose & 7; // user override
			}
			disp <<= 2;

			// packed fields
			ms.WriteByte(Convert.ToByte(0 | // 1:3 reserved
				disp | // 4:6 disposal
				0 | // 7   user input - 0 = none
				transp)); // 8   transparency flag

			WriteShort(delay); // delay x 1/100 sec
			ms.WriteByte(Convert.ToByte(transIndex)); // transparent color index
			ms.WriteByte(0); // block terminator
		}

		void WriteImageDesc()
		{
			ms.WriteByte(0x2c); // image separator
			WriteShort(0); // image position x,y = 0,0
			WriteShort(0);
			WriteShort(width); // image size
			WriteShort(height);
			// no LCT  - GCT is used for first (or only) frame
			ms.WriteByte(0);

		}

		void WriteLSD()
		{
			// logical screen size
			WriteShort(width);
			WriteShort(height);
			// packed fields
			ms.WriteByte(Convert.ToByte(0x80 | // 1   : global color table flag = 1 (gct used)
				0x70 | // 2-4 : color resolution = 7
				0x00 | // 5   : gct sort flag = 0
				palSize)); // 6-8 : gct size

			ms.WriteByte(0); // background color index
			ms.WriteByte(0); // pixel aspect ratio - assume 1:1
		}

		void WriteNetscapeExt()
		{
			ms.WriteByte(0x21); // extension introducer
			ms.WriteByte(0xff); // app extension label
			ms.WriteByte(11); // block size
			WriteString("NETSCAPE" + "2.0"); // app id + auth code
			ms.WriteByte(3); // sub-block size
			ms.WriteByte(1); // loop sub-block id
			WriteShort(repeat); // loop count (extra iterations, 0=repeat forever)
			ms.WriteByte(0); // block terminator
		}

		void WritePalette()
		{
			ms.Write(colorTab, 0, colorTab.Length);
			int n = (3 * 256) - colorTab.Length;
			for (int i = 0; i < n; i++)
			{
				ms.WriteByte(0);
			}
		}

		void WritePixels()
		{
			LZWEncoder encoder = new LZWEncoder(width, height, indexedPixels, colorDepth);
			encoder.Encode(ms);
		}

		void WriteShort(int value)
		{
			ms.WriteByte(Convert.ToByte(value & 0xff));
			ms.WriteByte(Convert.ToByte((value >> 8) & 0xff));
		}

		void WriteString(String s)
		{
			char[] chars = s.ToCharArray();
			for (int i = 0; i < chars.Length; i++)
			{
				ms.WriteByte((byte)chars[i]);
			}
		}

		int delay = 0;
		int width;
		int height;
		int transIndex;
		bool started = false;
		MemoryStream ms;
		Color32[] pixels;
		byte[] indexedPixels;
		byte[] prevIndexedPixels;
		int colorDepth;
		byte[] colorTab;
		bool[] usedEntry = new bool[256]; // active palette entries
		int palSize = 7; // color table size (bits-1)
		bool firstFrame = true;
		NeuQuant nq;
	}

}