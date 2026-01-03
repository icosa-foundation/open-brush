using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpQuill
{
  /// <summary>
  /// Layer types.
  /// </summary>
  public enum LayerType
  {
    /// <summary>
    /// Unknown layer type.
    /// </summary>
    Unknown,

    /// <summary>
    /// A group layer contains other layers.
    /// </summary>
    Group,

    /// <summary>
    /// A paint layer contains paint strokes.
    /// </summary>
    Paint,

    /// <summary>
    /// A sound layer contains an audio file.
    /// </summary>
    Sound,

    /// <summary>
    /// A picture layer contains an image file.
    /// </summary>
    Picture,

    /// <summary>
    /// A viewpoint layer contains a viewpoint.
    /// </summary>
    Viewpoint,

    /// <summary>
    /// A model layer contains a mesh.
    /// </summary>
    Model,

    /// <summary>
    /// A camera layer.
    /// </summary>
    Camera,
  }
}
