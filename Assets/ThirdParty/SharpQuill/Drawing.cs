using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpQuill
{
  /// <summary>
  /// Represents a drawing.
  /// A drawing is a collection of paint strokes at a specific keyframe.
  /// </summary>
  public class Drawing
  {
    /// <summary>
    /// The bounding box of the whole drawing.
    /// </summary>
    public BoundingBox BoundingBox { get; set; }

    /// <summary>
    /// The original file offset of this drawing in the qbin.
    /// This is only modified on import or export.
    /// </summary>
    public long DataFileOffset { get; set; }

    /// <summary>
    /// The drawing data for this drawing.
    /// </summary>
    public DrawingData Data { get; set; } = new DrawingData();

    /// <summary>
    /// Performs a deep copy of this drawing.
    /// Note that the new drawing will still contain the original file offset to the data.
    /// The file offset will be updated after exporting the cloned drawing to the qbin.
    /// </summary>
    public Drawing Clone()
    {
      Drawing d = new Drawing();
      d.BoundingBox = BoundingBox;
      d.DataFileOffset = DataFileOffset;
      d.Data = Data.Clone();
      return d;
    }

    /// <summary>
    /// Updates the bounding box of the drawing.
    /// </summary>
    /// <param name="updateStrokes">Whether to update the bounding box of the contained strokes. You can set this to false if the strokes were already updated.</param>
    public void UpdateBoundingBox(bool updateStrokes)
    {
      BoundingBox.Reset();
      foreach (Stroke s in Data.Strokes)
      {
        if (updateStrokes)
          s.UpdateBoundingBox();

        BoundingBox.Expand(s.BoundingBox);
      }
    }
  }
}
