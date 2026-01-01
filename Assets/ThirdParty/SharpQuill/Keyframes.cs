using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpQuill
{
  /// <summary>
  /// The keyframes contain the various channels of animation for each layer.
  /// This is for tweened animation, the frame by frame animation happen in the drawing sequence of the paint layer.
  /// Times are relative to the parent sequence, they can be negative if the keyframe is before the parent offset.
  /// It is not clear if the visibility and offset keyframes are really animable,
  /// they seem to only be used for offsetting the layer to the right.
  /// In this case the visibility keyframe is true at the start time, with interpolation set to None,
  /// and the offset keyframe is set to 0 at the start time.
  /// </summary>
  public class Keyframes
  {
    /// <summary>
    /// List of visibility keyframes. Not clear if this is really animable.
    /// </summary>
    public List<Keyframe<bool>> Visibility { get; set; } = new List<Keyframe<bool>>();

    /// <summary>
    /// List of offset keyframes. Not clear if this is really animable.
    /// </summary>
    public List<Keyframe<int>> Offset { get; set; } = new List<Keyframe<int>>();

    /// <summary>
    /// List of opacity keyframes.
    /// </summary>
    public List<Keyframe<float>> Opacity { get; set; } = new List<Keyframe<float>>();

    /// <summary>
    /// List of transform keyframes.
    /// </summary>
    public List<Keyframe<Transform>> Transform { get; set; } = new List<Keyframe<Transform>>();

    public Keyframes()
    {
      // The minimal working file requires a visibility and offset keyframes.
      Visibility.Add(new Keyframe<bool>(0, true, Interpolation.None));
      Offset.Add(new Keyframe<int>(0, 0, Interpolation.None));
    }
  }
}
