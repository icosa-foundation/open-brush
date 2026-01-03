using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpQuill
{
  /// <summary>
  /// Contains the list of strokes making up this drawing.
  /// </summary>
  public class DrawingData
  {
    public List<Stroke> Strokes { get; set; } = new List<Stroke>();

    /// <summary>
    /// Performs a deep copy of this drawing data.
    /// </summary>
    public DrawingData Clone()
    {
      DrawingData d = new DrawingData();
      foreach (Stroke stroke in Strokes)
        d.Strokes.Add(stroke.Clone());

      return d;
    }
  }
}
