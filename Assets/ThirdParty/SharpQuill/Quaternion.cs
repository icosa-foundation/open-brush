using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpQuill
{
  /// <summary>
  /// A 4D vector representing a rotation.
  /// </summary>
  public struct Quaternion
  {
    public float X;
    public float Y;
    public float Z;
    public float W;

    private static Quaternion identity = new Quaternion(0, 0, 0, 1);
    public static Quaternion Identity { get { return identity; } }


    public Quaternion(float x, float y, float z, float w)
    {
      X = x;
      Y = y;
      Z = z;
      W = w;
    }

    /// <summary>
    /// Construct a quaternion from a list of floats interpreted as [X, Y, Z, W].
    /// </summary>
    public Quaternion(List<float> value)
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
