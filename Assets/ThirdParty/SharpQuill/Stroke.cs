using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpQuill
{
  /// <summary>
  /// Represents a paint stroke. A stroke is a series of vertices and one brush type.
  /// </summary>
  public class Stroke
  {
    /// <summary>
    /// Stroke Id.
    /// Not too sure about the uniqueness requirement for this.
    /// There are issues with large values in some files.
    /// </summary>
    public UInt32 Id {get; set;} = 0;

    /// <summary>
    /// This value is imported from the qbin file but its purpose is unknown.
    /// </summary>
    public int u2 { get; set; } = 0;

    /// <summary>
    /// The bounding box of the stroke.
    /// </summary>
    public BoundingBox BoundingBox { get; set; } = new BoundingBox();

    /// <summary>
    /// The type of brush used for this stroke.
    /// </summary>
    public BrushType BrushType { get; set; } = BrushType.Cylinder;

    /// <summary>
    /// Whether rotational opacity is enabled on this stroke.
    /// Rotational opacity makes vertices 100% visible when seen from their tangent,
    /// and progressively fades to invisibility when seen from the sides or the back.
    /// </summary>
    public bool DisableRotationalOpacity { get; set; } = true;

    /// <summary>
    /// This value is imported from the qbin file but its purpose is unknown.
    /// </summary>
    public byte u3 { get; set; }

    /// <summary>
    /// List of vertices making up this stroke.
    /// </summary>
    public List<Vertex> Vertices { get; set; } = new List<Vertex>();

    /// <summary>
    /// Performs a deep copy of this stroke.
    /// </summary>
    public Stroke Clone()
    {
      Stroke s = new Stroke();
      s.Id = Id;
      s.u2 = u2;
      s.BoundingBox = BoundingBox;
      s.BrushType = BrushType;
      s.DisableRotationalOpacity = DisableRotationalOpacity;
      s.u3 = u3;

      foreach (Vertex vertex in Vertices)
        s.Vertices.Add(vertex);

      return s;
    }

    /// <summary>
    /// Updates the bounding of the stroke.
    /// </summary>
    public void UpdateBoundingBox()
    {
      BoundingBox.Reset();
      foreach (Vertex v in Vertices)
        BoundingBox.Expand(v.Position);
    }
  }
}
