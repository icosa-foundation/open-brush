using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpQuill
{
  /// <summary>
  /// Represents 3D transform.
  /// </summary>
  public struct Transform
  {
    public Quaternion Rotation;
    public float Scale;
    public string Flip;
    public Vector3 Translation;

    private static Transform identity = new Transform(new Quaternion(0, 0, 0, 1), 1.0f, "N", new Vector3(0, 0, 0));
    public static Transform Identity { get { return identity; } }

    public Transform(Quaternion rotation, float scale, string flip, Vector3 translation)
    {
      Rotation = rotation;
      Scale = scale;
      Flip = flip;
      Translation = translation;
    }
  }
}
