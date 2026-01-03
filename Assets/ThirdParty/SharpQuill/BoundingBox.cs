using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpQuill
{
  /// <summary>
  /// Axis-aligned bounding box.
  /// </summary>
  public struct BoundingBox
  {
    public const float HUGE = 1.0e+20f;

    public float MinX;
    public float MaxX;
    public float MinY;
    public float MaxY;
    public float MinZ;
    public float MaxZ;

    public BoundingBox(float minX, float maxX, float minY, float maxY, float minZ, float maxZ)
    {
      this.MinX = minX;
      this.MaxX = maxX;
      this.MinY = minY;
      this.MaxY = maxY;
      this.MinZ = minZ;
      this.MaxZ = maxZ;
    }

    /// <summary>
    /// Construct a bounding box from a list of floats interpreted as [minX, maxX, minY, maxY, minZ, maxZ].
    /// </summary>
    public BoundingBox(List<float> value)
      : this()
    {
      if (value.Count != 6)
        return;

      MinX = value[0];
      MaxX = value[1];
      MinY = value[2];
      MaxY = value[3];
      MinZ = value[4];
      MaxZ = value[5];
    }

    public override string ToString()
    {
      return string.Format(CultureInfo.InvariantCulture, "{0}, {1}, {2}, {3}, {4}, {5}", 
        MinX, MaxX, MinY, MaxY, MinZ, MaxZ);
    }

    /// <summary>
    /// Resets the bounding box to a reverted box.
    /// </summary>
    public void Reset()
    {
      MinX = MinY = MinZ = HUGE;
      MaxX = MaxY = MaxZ = -HUGE; 
    }

    /// <summary>
    /// Returns the center of the bounding box.
    /// </summary>
    public Vector3 Center()
    {
      return new Vector3(MinX + (MaxX - MinX)/2, MinY + (MaxY - MinY)/2, MinZ + (MaxZ - MinZ)/2);
    }

    /// <summary>
    /// Expand the bounding box so that it also contains the passed one.
    /// </summary>
    public void Expand(BoundingBox b)
    {
      MinX = Math.Min(MinX, b.MinX);
      MinY = Math.Min(MinY, b.MinY);
      MinZ = Math.Min(MinZ, b.MinZ);
      MaxX = Math.Max(MaxX, b.MaxX);
      MaxY = Math.Max(MaxY, b.MaxY);
      MaxZ = Math.Max(MaxZ, b.MaxZ);
    }

    /// <summary>
    /// Expand the bounding box so that it also contains the passed vector.
    /// </summary>
    public void Expand(Vector3 v)
    {
      MinX = Math.Min(MinX, v.X);
      MinY = Math.Min(MinY, v.Y);
      MinZ = Math.Min(MinZ, v.Z);
      MaxX = Math.Max(MaxX, v.X);
      MaxY = Math.Max(MaxY, v.Y);
      MaxZ = Math.Max(MaxZ, v.Z);
    }
  }
}
