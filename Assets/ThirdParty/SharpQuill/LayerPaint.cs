using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpQuill
{
  /// <summary>
  /// The type-specific data of a paint layer.
  /// A paint layer contains drawings on keyframes.
  /// </summary>
  public class LayerPaint : Layer
  {
    public override LayerType Type { get { return LayerType.Paint; } }

    /// <summary>
    /// The framerate for animated layers.
    /// </summary>
    public int Framerate { get; set; } = 24;

    /// <summary>
    /// Number of loops of animation. 0 means inifinite loops.
    /// </summary>
    public int MaxRepeatCount { get; set; } = 0;

    /// <summary>
    /// The list of drawings (keyframes) in this layer.
    /// Empty keyframes are still drawings.
    /// Non-keyframes are not drawings.
    /// Inserted keyframes are appended at the end of the list.
    /// </summary>
    public List<Drawing> Drawings { get; set; } = new List<Drawing>();

    /// <summary>
    /// The list of frames in the layer, values are indices into the Drawings list.
    /// A non-keyframe means an index is repeated.
    /// The values are not necessarily in order if we have inserted a keyframe in the middle.
    /// </summary>
    public List<int> Frames { get; set; } = new List<int>();

    /// <summary>
    /// Constructs a new paint layer.
    /// </summary>
    /// <param name="name">The name of the layer.</param>
    /// <param name="addDrawing">Whether a drawing is immediately added to the layer. All paint layers need to have at least one drawing.</param>
    public LayerPaint(string name = "", bool addDrawing = false)
    {
      this.Name = name;

      if (addDrawing)
      {
        Drawings.Add(new Drawing());
        Frames.Add(0);
      }
    }
  }
}
