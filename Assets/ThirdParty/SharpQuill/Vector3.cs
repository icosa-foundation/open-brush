using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpQuill
{
  /// <summary>
  /// Represents a 3D vector.
  /// </summary>
  public struct Vector3
  {
    public float X;
    public float Y;
    public float Z;

    public Vector3(float x, float y, float z)
    {
      X = x;
      Y = y;
      Z = z;
    }

    /// <summary>
    /// Construct a 3D vector from a list of floats interpreted as [X, Y, Z].
    /// </summary>
    public Vector3(List<float> value)
      : this()
    {
      if (value.Count != 3)
        return;

      X = value[0];
      Y = value[1];
      Z = value[2];
    }
  }
}
