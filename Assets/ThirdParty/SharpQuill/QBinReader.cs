using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpQuill
{
  /// <summary>
  /// Binary reader for qbin files.
  /// </summary>
  public class QBinReader : BinaryReader
  {
    public QBinReader(Stream stream)
			: base(stream)
		{
    }

    /// <summary>
    /// Reads the paint stroke data for one drawing.
    /// </summary>
    public DrawingData ReadDrawingData()
    {
      DrawingData pl = new DrawingData();

      int count = ReadInt32();
      pl.Strokes = new List<Stroke>();
      for (int i = 0; i < count; i++)
        pl.Strokes.Add(ReadStroke());
      
      return pl;
    }
    
    private Stroke ReadStroke()
    {
      Stroke stroke = new Stroke();

      stroke.Id = ReadUInt32();
      stroke.u2 = ReadInt32();
      stroke.BoundingBox = ReadBoundingBox();
      short brushType = ReadInt16();
      stroke.BrushType = (Enum.IsDefined(typeof(BrushType), brushType)) ? (BrushType)brushType : BrushType.Cylinder;
      stroke.DisableRotationalOpacity = ReadBoolean();
      stroke.u3 = ReadByte();
      int count = ReadInt32();
      for (int i = 0; i < count; i++)
        stroke.Vertices.Add(ReadVertex());
      
      return stroke;
    }

    private BoundingBox ReadBoundingBox()
    {
      BoundingBox bbox = new BoundingBox();

      bbox.MinX = ReadSingle();
      bbox.MaxX = ReadSingle();
      bbox.MinY = ReadSingle();
      bbox.MaxY = ReadSingle();
      bbox.MinZ = ReadSingle();
      bbox.MaxZ = ReadSingle();

      return bbox;
    }

    private Vertex ReadVertex()
    {
      Vertex v = new Vertex();

      v.Position = ReadVector3();
      v.Normal = ReadVector3();
      v.Tangent = ReadVector3();
      v.Color = ReadColor();
      v.Opacity = ReadSingle();
      v.Width = ReadSingle();
      
      return v;
    }

    private Color ReadColor()
    {
      Color c = new Color();

      c.R = ReadSingle();
      c.G = ReadSingle();
      c.B = ReadSingle();

      return c;
    }

    private Vector3 ReadVector3()
    {
      Vector3 v = new Vector3();

      v.X = ReadSingle();
      v.Y = ReadSingle();
      v.Z = ReadSingle();

      return v;
    }
  }
}
