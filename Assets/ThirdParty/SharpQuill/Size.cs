using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpQuill
{
  /// <summary>
  /// Represents a 2D size.
  /// </summary>
  public struct Size
  {
    public int Width;
    public int Height;

    public Size(int w, int h)
    {
      Width = w;
      Height = h;
    }

    /// <summary>
    /// Construct a size from a list of ints interpreted as [Width, Height].
    /// </summary>
    public Size(List<int> value)
      : this()
    {
      if (value.Count != 2)
        return;

      Width = value[0];
      Height = value[1];
    }
  }
}
