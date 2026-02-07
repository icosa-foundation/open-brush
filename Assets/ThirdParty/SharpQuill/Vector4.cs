using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpQuill
{
  /// <summary>
  /// A 4D vector.
  /// </summary>
  public struct Vector4
  {
    public float X;
    public float Y;
    public float Z;
    public float W;

    public Vector4(float x, float y, float z, float w)
    {
      X = x;
      Y = y;
      Z = z;
      W = w;
    }

    /// <summary>
    /// Construct a vector4 from a list of floats interpreted as [X, Y, Z, W].
    /// </summary>
    public Vector4(List<float> value)
      : this()
    {
      if (value.Count != 4)
        return;

      X = value[0];
      Y = value[1];
      Z = value[2];
      W = value[3];
    }
  }
}
