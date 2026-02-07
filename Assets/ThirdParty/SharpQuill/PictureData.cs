using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpQuill
{
  public class PictureData
  {
    public bool HasAlpha { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public byte[] Pixels { get; set; }
  }
}
