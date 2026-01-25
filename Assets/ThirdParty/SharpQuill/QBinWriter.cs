using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpQuill
{
  /// <summary>
  /// Binary writer for qbin files.
  /// </summary>
  public class QBinWriter : BinaryWriter
  {
    public QBinWriter(Stream stream)
      : base(stream)
    {
    }
    
    public void Write(DrawingData value)
    {
      Write(value.Strokes.Count);
      foreach (Stroke stroke in value.Strokes)
        Write(stroke);
    }

    public void Write(Stroke stroke)
    {
      Write(stroke.Id);
      Write(stroke.u2);
      Write(stroke.BoundingBox);
      Write((UInt16)stroke.BrushType);
      Write(stroke.DisableRotationalOpacity);
      Write(stroke.u3);
      Write(stroke.Vertices.Count);
      foreach (Vertex vertex in stroke.Vertices)
        Write(vertex);
    }

    public void Write(BoundingBox bbox)
    {
      Write(bbox.MinX);
      Write(bbox.MaxX);
      Write(bbox.MinY);
      Write(bbox.MaxY);
      Write(bbox.MinZ);
      Write(bbox.MaxZ);
    }

    public void Write(Vertex vertex)
    {
      Write(vertex.Position);
      Write(vertex.Normal);
      Write(vertex.Tangent);
      Write(vertex.Color);
      Write(vertex.Opacity);
      Write(vertex.Width);
    }

    public void Write(Vector3 v)
    {
      Write(v.X);
      Write(v.Y);
      Write(v.Z);
    }

    public void Write(Color c)
    {
      Write(c.R);
      Write(c.G);
      Write(c.B);
    }
  }
}
