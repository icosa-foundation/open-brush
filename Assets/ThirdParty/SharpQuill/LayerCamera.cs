using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpQuill
{
  /// <summary>
  /// The type-specific data of a camera layer.
  /// </summary>
  public class LayerCamera : Layer
  {
    public override LayerType Type { get { return LayerType.Camera; } }

    /// <summary>
    /// Field of view of the camera.
    /// </summary>
    public float FOV { get; set; } = 19;

    /// <summary>
    /// Constructs a new camera layer.
    /// </summary>
    /// <param name="name">The name of the layer.</param>
    public LayerCamera(string name = "")
    {
      this.Name = name;
    }
  }
}
