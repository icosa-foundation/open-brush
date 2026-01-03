using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpQuill
{
  /// <summary>
  /// The type-specific data of a viewpoint layer.
  /// </summary>
  public class LayerViewpoint : Layer
  {
    public override LayerType Type { get { return LayerType.Viewpoint; } }
    public int Version { get; set; }
    public Color Color { get; set; }
    public Vector4 Sphere { get; set; }
    public bool AllowTranslationX { get; set; }
    public bool AllowTranslationY { get; set; }
    public bool AllowTranslationZ { get; set; }
    public bool Exporting { get; set; }
    public bool ShowingVolume { get; set; }

    /// <summary>
    /// The type of viewpoint. Possible values are "FloorLevel" and "EyeLevel".
    /// </summary>
    public string TypeStr { get; set; } = "FloorLevel";

    /// <summary>
    /// Constructs a new viewpoint layer.
    /// </summary>
    /// <param name="name">The name of the layer.</param>
    public LayerViewpoint(string name = "")
    {
      this.Name = name;
    }

    /// <summary>
    /// Creates the default viewpoint layer used in the default sequence.
    /// </summary>
    public static LayerViewpoint CreateDefault()
    {
      LayerViewpoint layer = new LayerViewpoint("InitialSpawnArea");

      layer.Visible = false;

      layer.Version = 1;
      layer.Color = new Color(0.113542f, 0.409455f, 0.808914f);
      layer.Sphere = new Vector4(0, 1, 0, 2);
      layer.AllowTranslationX = true;
      layer.AllowTranslationY = true;
      layer.AllowTranslationZ = true;
      layer.Exporting = true;
      layer.ShowingVolume = false;
      layer.TypeStr = "FloorLevel";

      return layer;
    }
  }
}
