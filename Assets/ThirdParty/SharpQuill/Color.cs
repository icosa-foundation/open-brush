using System.Collections.Generic;
using System.Globalization;

namespace SharpQuill
{
  /// <summary>
  /// Represents an RGB color.
  /// </summary>
  public struct Color
  {
    public float R;
    public float G;
    public float B;

    public Color(float r, float g, float b)
    {
      R = r;
      G = g;
      B = b;
    }

    /// <summary>
    /// Construct a color from a list of floats interpreted as [R, G, B].
    /// </summary>
    public Color(List<float> value)
      : this()
    {
      if (value.Count != 3)
        return;

      R = value[0];
      G = value[1];
      B = value[2];
    }

    public override string ToString()
    {
      return string.Format(CultureInfo.InvariantCulture, "R:{0} G:{1} B:{2}", R, G, B);
    }

  }
}
