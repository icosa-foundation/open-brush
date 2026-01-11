using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpQuill
{
  /// <summary>
  /// Represents a single vertex in a stroke.
  /// A vertex is a point along a paint stroke and has various attributes like color or opacity.
  /// </summary>
  public struct Vertex
  {
    /// <summary>
    /// Spatial position of the vertex. 
    /// </summary>
    public Vector3 Position;

    /// <summary>
    /// Normal of the vertex. This is used to orient ribbon brushes.
    /// </summary>
    public Vector3 Normal;

    /// <summary>
    /// Tangent of the vertex. This is used to define the incident ray for rotational opacity.
    /// </summary>
    public Vector3 Tangent;

    /// <summary>
    /// Color of the vertex.
    /// </summary>
    public Color Color;

    /// <summary>
    /// Opacity of the vertex.
    /// </summary>
    public float Opacity;

    /// <summary>
    /// Width of the vertex.
    /// Combined with the brush type of the stroke this is used to define the shape of the stroke at that point.
    /// </summary>
    public float Width;

    public Vertex(Vector3 position, Vector3 normal, Vector3 tangent, Color color, float opacity, float width)
    {
      this.Position = position;
      this.Normal = normal;
      this.Tangent = tangent;
      this.Color = color;
      this.Opacity = opacity;
      this.Width = width;
    }
  }
}
