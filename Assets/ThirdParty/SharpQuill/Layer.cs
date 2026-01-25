using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpQuill
{
  /// <summary>
  /// Layer data that is common to all layer types.
  /// </summary>
  public abstract class Layer
  {
    /// <summary>
    /// The name of the layer.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Whether the layer is visible.
    /// </summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    /// Whether the layer is locked.
    /// Locked layers cannot be modfied or expanded.
    /// </summary>
    public bool Locked { get; set; } = false;

    /// <summary>
    /// Whether the layer is collapsed.
    /// </summary>
    public bool Collapsed { get; set; } = false;

    /// <summary>
    /// Whether the bounding box of the layer is visible.
    /// </summary>
    public bool BBoxVisible { get; set; } = false;

    /// <summary>
    /// Layer-specific opacity level.
    /// </summary>
    public float Opacity { get; set; } = 1.0f;

    /// <summary>
    /// The type of the layer.
    /// </summary>
    public abstract LayerType Type { get; }

    /// <summary>
    /// Whether this layer is the root of a model hierarchy.
    /// </summary>
    public bool IsModelTopLayer { get; set; } = false;

    public KeepAlive KeepAlive { get; set; } = new KeepAlive();

    /// <summary>
    /// The local coordinate system in relation to the parent layer.
    /// </summary>
    public Transform Transform { get; set; } = Transform.Identity;

    /// <summary>
    /// The transform of the pivot for this layer, in layer space.
    /// </summary>
    public Transform Pivot { get; set; } = Transform.Identity;

    /// <summary>
    /// The animation channels for this layer (for interpolated animation).
    /// </summary>
    public Animation Animation { get; set; } = new Animation();
  }
}
