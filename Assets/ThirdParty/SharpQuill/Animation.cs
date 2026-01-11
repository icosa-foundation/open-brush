using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpQuill
{
  /// <summary>
  /// The animation contains the list of keyframes for a layer.
  /// This is for tweened animation, the frame by frame animation happen in the drawing sequence of the paint layer.
  /// </summary>
  public class Animation
  {
    /// <summary>
    /// Duration of the animation for sequence groups.
    /// This value is only relevant for group layers.
    /// </summary>
    public float Duration { get; set; }

    /// <summary>
    /// Whether a group is a sequence.
    /// This value is only relevant for group layers.
    /// </summary>
    public bool Timeline { get; set; }

    public float StartOffset { get; set; }

    public float MaxRepeatCount { get; set; }

    /// <summary>
    /// The collection of animated channels and their values.
    /// </summary>
    public Keyframes Keys { get; set; } = new Keyframes();
  }
}
